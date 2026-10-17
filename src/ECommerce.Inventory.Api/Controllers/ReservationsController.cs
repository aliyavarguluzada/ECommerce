using ECommerce.Inventory.Application;
using ECommerce.Inventory.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Inventory.Api.Controllers;

[ApiController]

[Authorize(Policy = "InventoryOperations")]

[Route("api/inventory/reservations")]

public sealed class ReservationsController(InventoryService service) : ControllerBase
{
    [HttpGet]

    public async Task<ActionResult<PagedResult<ReservationResponse>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReservationStatus? status = null,
        CancellationToken ct = default) => Ok(
        await service.ReservationsAsync(pageNumber, pageSize, status, ct));

    [HttpGet("{orderId:guid}")]

    public async Task<ActionResult<ReservationResponse>> Get(
        Guid orderId,
        CancellationToken ct) => Ok(
        await service.GetReservationAsync(orderId, ct));

    [HttpPost]

    public async Task<ActionResult<ReservationResponse>> Reserve(
        ReserveStockCommand command,
        CancellationToken ct) => Ok(
        await service.ReserveAsync(command, ct));

    [HttpPost("{orderId:guid}/commit")]

    public async Task<ActionResult<ReservationResponse>> Commit(
        Guid orderId,
        CancellationToken ct) => Ok(
        await service.CommitAsync(orderId, ct));

    [HttpPost("{orderId:guid}/release")]

    public async Task<ActionResult<ReservationResponse>> Release(
        Guid orderId,
        CancellationToken ct) => Ok(
        await service.ReleaseAsync(orderId, ct));

}
