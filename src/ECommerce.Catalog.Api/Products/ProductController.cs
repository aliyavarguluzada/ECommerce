using ECommerce.Catalog.Application;
using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Catalog.Api.Products;

[ApiController]

[Route("api/products")]

public sealed class ProductController(ICatalogService service) : ControllerBase
{
    [HttpGet]

    [ProducesResponseType<PagedResult<ProductResponse>>(StatusCodes.Status200OK)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<PagedResult<ProductResponse>>> GetAll(
        [FromQuery] ProductFilter query,
        CancellationToken cancellationToken) => Ok(
        await service.GetProductsAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]

    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await service.GetProductAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest command,
        CancellationToken cancellationToken)
    {
        var product = await service.CreateProductAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> Update(
        Guid id,
        UpdateProductRequest command,
        CancellationToken cancellationToken) => await service.UpdateProductAsync(
        id,
        command,
        cancellationToken) ? NoContent() : NotFound();

    [HttpPatch("{id:guid}/price")]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> ChangePrice(
        Guid id,
        ChangeProductPriceRequest command,
        CancellationToken cancellationToken) => await service.ChangeProductPriceAsync(
        id,
        command,
        cancellationToken) ? NoContent() : NotFound();

    // Retain the product for historical orders and inventory references.

    [HttpDelete("{id:guid}")]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> Discontinue(
        Guid id,
        CancellationToken cancellationToken) => await service.DiscontinueProductAsync(
        id,
        cancellationToken) ? NoContent() : NotFound();

}
