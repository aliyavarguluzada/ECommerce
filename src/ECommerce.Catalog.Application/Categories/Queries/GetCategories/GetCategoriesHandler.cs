using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Categories.Queries.GetCategories;

public sealed class GetCategoriesHandler(ICategoryRepository repository)
{
    public async Task<IReadOnlyList<CategoryResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var categories = await repository.GetAllAsync(cancellationToken);
        return categories.Select(CategoryResponse.From).ToArray();
    }
}
