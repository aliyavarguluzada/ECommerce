using ECommerce.Catalog.Domain.Aggregates.Categories;
namespace ECommerce.Catalog.Application.Categories;

public sealed record CategoryResponse(Guid Id, string Name, string? Description, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static CategoryResponse From(Category category) =>
        new(category.Id, category.Name, category.Description, category.CreatedAt, category.UpdatedAt);
}
