using ECommerce.Order.Application;
using ECommerce.Order.Domain;
using ECommerce.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Order.Api.Controllers;

public sealed record CreateCustomerOrder(
    Guid RequestId,
    AddressRequest ShippingAddress,
    IReadOnlyList<OrderItemRequest> Items);

[ApiController, Route("api/orders")]

public sealed class OrdersController(OrderService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "Customer")]

    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null,
        [FromQuery] OrderStatus? status = null,
        CancellationToken ct = default) => Ok(
        await service.ListAsync(
            pageNumber,
            pageSize,
            User.IsInRole("Admin") ? customerId : User.CustomerId(),
            status,
            ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<OrderResponse>> Get(Guid id, CancellationToken ct) => Ok(await Owned(id, ct));

    [HttpPost, Authorize(Policy = "Customer")]

    public async Task<ActionResult<OrderResponse>> Create(
        CreateCustomerOrder command,
        CancellationToken ct) => Created(
        await service.CreateAsync(
            new(command.RequestId, User.CustomerId(), command.ShippingAddress, command.Items),
            ct));

    [HttpPost("internal"), Authorize(Policy = "CartService")]

    public async Task<ActionResult<OrderResponse>> InternalCreate(
        CreateOrderCommand command,
        CancellationToken ct) => Created(
        await service.CreateAsync(command, ct));

    [HttpGet("internal/{id:guid}"), Authorize(Policy = "OrderReaders")]

    public async Task<ActionResult<OrderSummary>> InternalGet(Guid id, CancellationToken ct)
    {
        var order = await service.GetAsync(id, ct);

        return Ok(new OrderSummary(order.Id, order.CustomerId, order.Status, order.Currency, order.Total));
    }

    [HttpPost("internal/{id:guid}/payment"), Authorize(Policy = "PaymentService")]

    public async Task<ActionResult<OrderResponse>> RecordPayment(
        Guid id,
        RecordPaymentCommand command,
        CancellationToken ct) => Ok(
        await service.RecordPaymentAsync(id, command, ct));

    [HttpPost("{id:guid}/confirm"), Authorize(Policy = "Admin")]

    public async Task<ActionResult<OrderResponse>> Confirm(
        Guid id,
        CancellationToken ct) => Ok(
        await service.ConfirmAsync(id, ct));

    [HttpPost("{id:guid}/cancel"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancelOrderCommand command, CancellationToken ct)
    {
        await Owned(id, ct);

        return Ok(await service.CancelAsync(id, command, ct));
    }

    [HttpPost("{id:guid}/retry"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<OrderResponse>> Retry(Guid id, CancellationToken ct)
    {
        await Owned(id, ct);

        return Ok(await service.RetryAsync(id, ct));
    }

    [HttpPost("{id:guid}/ship"), Authorize(Policy = "Admin")]

    public async Task<ActionResult<OrderResponse>> Ship(
        Guid id,
        ShipOrderCommand command,
        CancellationToken ct) => Ok(
        await service.ShipAsync(id, command, ct));

    [HttpPost("{id:guid}/deliver"), Authorize(Policy = "Admin")]

    public async Task<ActionResult<OrderResponse>> Deliver(
        Guid id,
        CancellationToken ct) => Ok(
        await service.DeliverAsync(id, ct));

    private ActionResult<OrderResponse> Created(
        OrderResponse order) => order.Status == OrderStatus.PendingInventory ? AcceptedAtAction(
        nameof(Get),
        new { id = order.Id },
        order) : CreatedAtAction(
        nameof(Get),
        new { id = order.Id },
        order);

    private async Task<OrderResponse> Owned(Guid id, CancellationToken ct)
    {
        var order = await service.GetAsync(id, ct);

        if (!User.IsInRole("Admin") && order.CustomerId != User.CustomerId())
        {
            throw new ServiceException(404, "Order not found.");

        }

        return order;
    }

}
