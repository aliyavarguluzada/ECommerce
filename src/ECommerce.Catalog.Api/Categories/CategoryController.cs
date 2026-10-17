using ECommerce.Catalog.Application;
using ECommerce.Catalog.Application.Categories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Catalog.Api.Categories;

[Route("api/categories")]

[ApiController]

public sealed class CategoryController(ICatalogService service) : ControllerBase
{
    [HttpGet]

    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]

    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        CancellationToken cancellationToken) => Ok(
        await service.GetCategoriesAsync(cancellationToken));

    [HttpPut("{id:guid}")]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> Update(
        Guid id,
        UpdateCategoryRequest command,
        CancellationToken cancellationToken) => await service.UpdateCategoryAsync(
        id,
        command,
        cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken) => await service.DeleteCategoryAsync(
        id,
        cancellationToken) ? NoContent() : NotFound();

    [HttpPost]

    [Authorize(Policy = "Admin")]

    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]

    public async Task<ActionResult<CategoryResponse>> Create(
        CreateCategoryRequest command,
        CancellationToken cancellationToken)
    {
        var category = await service.CreateCategoryAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpGet("{id:guid}")]

    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await service.GetCategoryAsync(id, cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

}
