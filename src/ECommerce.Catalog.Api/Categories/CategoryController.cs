using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Categories.Commands.CreateCategory;
using ECommerce.Catalog.Application.Categories.Queries.GetCategoryById;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ECommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using ECommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using ECommerce.Catalog.Application.Categories.Queries.GetCategories;
namespace ECommerce.Catalog.Api.Categories;

[Route("api/categories")]
[ApiController]
public sealed class CategoryController(
    CreateCategoryHandler createHandler, GetCategoryByIdHandler getByIdHandler,
    GetCategoriesHandler listHandler, UpdateCategoryHandler updateHandler,
    DeleteCategoryHandler deleteHandler) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await listHandler.HandleAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryCommand command, CancellationToken cancellationToken) =>
        await updateHandler.HandleAsync(id, command, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await deleteHandler.HandleAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(
        CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await createHandler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await getByIdHandler.HandleAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }
}
