using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Categories.Queries.GetCategoryById;

public sealed class GetCategoryByIdHandler(ICategoryRepository repository)
{
    public async Task<CategoryResponse?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetByIdAsync(id, cancellationToken);

        return category is null ? null : CategoryResponse.From(category);
    }

}
