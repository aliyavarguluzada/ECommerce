using ECommerce.Inventory.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Inventory.Api.Controllers;

[ApiController]

[Authorize(Policy = "Admin")]

[Route("api/inventory/stock")]

public sealed class StockController(InventoryService service) : ControllerBase
{
    [HttpGet]

    public async Task<ActionResult<PagedResult<StockResponse>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? maxAvailable = null,
        CancellationToken ct = default) => Ok(
        await service.ListAsync(pageNumber, pageSize, maxAvailable, ct));

    [HttpGet("{productId:guid}")]

    public async Task<ActionResult<StockResponse>> Get(
        Guid productId,
        CancellationToken ct) => Ok(
        await service.GetAsync(productId, ct));

    [HttpPost]

    public async Task<ActionResult<StockResponse>> Create(CreateStockCommand command, CancellationToken ct)
    {
        var stock = await service.CreateAsync(command, ct);

        return CreatedAtAction(nameof(Get), new { productId = stock.ProductId }, stock);
    }

    [HttpPost("{productId:guid}/adjustments")]

    public async Task<ActionResult<StockResponse>> Adjust(
        Guid productId,
        AdjustStockCommand command,
        CancellationToken ct) => Ok(
        await service.AdjustAsync(productId, command, ct));

    [HttpGet("{productId:guid}/adjustments")]

    public async Task<ActionResult<PagedResult<AdjustmentResponse>>> Adjustments(
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => Ok(
        await service.AdjustmentsAsync(productId, pageNumber, pageSize, ct));

}
