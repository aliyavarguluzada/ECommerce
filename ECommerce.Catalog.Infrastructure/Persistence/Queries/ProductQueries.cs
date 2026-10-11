using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Products.Queries.GetProducts;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Catalog.Infrastructure.Persistence.Queries;

public sealed class ProductQueries(CatalogDbContext db) : IProductQueries
{
    public Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedResult<ProductResponse>> GetAllAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
    {
        query.Validate();
        var products = Filter(query);
        var count = await products.CountAsync(cancellationToken);
        var items = await products.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(items, count, query.PageNumber, query.PageSize);
    }

    internal IQueryable<ProductResponse> Filter(GetProductsQuery query)
    {
        query.Validate();
        var products = Project();
        if (query.CategoryId is { } categoryId) products = products.Where(x => x.CategoryId == categoryId);
        var activeFilter = query.IsActive ?? (query.IncludeInactive ? (bool?)null : true);
        if (activeFilter is { } isActive) products = products.Where(x => x.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            products = products.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        }
        return products;
    }

    internal IQueryable<ProductResponse> Project() =>
        from product in db.Products.AsNoTracking()
        join category in db.Categories.AsNoTracking() on product.CategoryId equals category.Id
        select new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price.Amount,
            Currency = product.Price.Currency,
            CategoryId = product.CategoryId,
            CategoryName = category.Name,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
