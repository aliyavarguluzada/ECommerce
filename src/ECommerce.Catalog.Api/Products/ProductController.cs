using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Products.Commands.CreateProduct;
using ECommerce.Catalog.Application.Products.Commands.UpdateProduct;
using ECommerce.Catalog.Application.Products.Commands.ChangeProductPrice;
using ECommerce.Catalog.Application.Products.Commands.DiscontinueProduct;
using ECommerce.Catalog.Application.Products.Queries.GetProductById;
using ECommerce.Catalog.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Catalog.Api.Products;

[ApiController]

[Route("api/products")]

public sealed class ProductController(
    CreateProductHandler createHandler,
    UpdateProductHandler updateHandler,
    ChangeProductPriceHandler priceHandler,
    DiscontinueProductHandler discontinueHandler,
    GetProductByIdHandler getHandler,
    GetProductsHandler listHandler) : ControllerBase
{
    [HttpGet]

    [ProducesResponseType<PagedResult<ProductResponse>>(StatusCodes.Status200OK)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<PagedResult<ProductResponse>>> GetAll(
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken) => Ok(
        await listHandler.HandleAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]

    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await getHandler.HandleAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await createHandler.HandleAsync(command, cancellationToken);

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
        UpdateProductCommand command,
        CancellationToken cancellationToken) => await updateHandler.HandleAsync(
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
        ChangeProductPriceCommand command,
        CancellationToken cancellationToken) => await priceHandler.HandleAsync(
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
        CancellationToken cancellationToken) => await discontinueHandler.HandleAsync(
        id,
        cancellationToken) ? NoContent() : NotFound();

}
