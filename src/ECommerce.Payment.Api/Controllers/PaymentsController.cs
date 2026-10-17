using ECommerce.Payment.Application;
using ECommerce.Payment.Domain;
using ECommerce.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Payment.Api.Controllers;

[ApiController, Route("api/payments")]

public sealed class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpPost, Authorize(Policy = "Customer")]

    public async Task<ActionResult<PaymentResponse>> Pay(PayRequest command, CancellationToken ct)
    {
        var payment = await service.PayAsync(User.CustomerId(), command, ct);

        if (payment.Status == PaymentStatus.Failed)
        {
            return StatusCode(
                402,
                new ProblemDetails { Status = 402, Title = "Payment declined", Detail = payment.FailureReason });
        }

        if (payment.Status is PaymentStatus.Pending or PaymentStatus.Refunding || (payment.Status == PaymentStatus.Succeeded && !payment.OrderNotified))
        {
            return AcceptedAtAction(nameof(Get), new { id = payment.Id }, payment);
        }

        return CreatedAtAction(nameof(Get), new { id = payment.Id }, payment);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "Customer")]

    public async Task<ActionResult<PaymentResponse>> Get(
        Guid id,
        CancellationToken ct) => Ok(
        await service.GetAsync(id, User.CustomerId(), User.IsInRole("Admin"), ct));

    [HttpGet, Authorize(Policy = "Customer")]

    public async Task<ActionResult<PagedResult<PaymentResponse>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null,
        CancellationToken ct = default) => Ok(
        await service.ListAsync(User.IsInRole("Admin") ? customerId : User.CustomerId(), pageNumber, pageSize, ct));

    [HttpPost("internal/orders/{orderId:guid}/refund"), Authorize(Policy = "OrderService")]

    public async Task<ActionResult<RefundSnapshot>> Refund(
        Guid orderId,
        CancellationToken ct) => Ok(
        await service.RefundOrderAsync(orderId, ct));

}
