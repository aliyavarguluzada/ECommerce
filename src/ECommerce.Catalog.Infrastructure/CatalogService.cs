using ECommerce.Catalog.Application;
using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Domain.ValueObjects;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Infrastructure;

public class CatalogService(CatalogDbContext db) : ICatalogService
{

    public async Task<CategoryResponse> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = Category.Create(request.Name, request.Description);

        if (await CategoryNameExistsAsync(category.Name, cancellationToken: cancellationToken))
        {
            throw new DuplicateCategoryException();
        }

        await AddCategoryAsync(category, cancellationToken);

        await SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await FindCategoryForUpdateAsync(id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (await CategoryHasProductsAsync(id, cancellationToken))
        {
            throw new CategoryInUseException();
        }

        RemoveCategory(category);

        await SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await FindCategoryForUpdateAsync(id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        category.UpdateDetails(request.Name, request.Description);

        if (await CategoryNameExistsAsync(category.Name, id, cancellationToken))
        {
            throw new DuplicateCategoryException();
        }

        await SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await ReadCategoriesAsync(cancellationToken);

        return categories.Select(CategoryResponse.From).ToArray();
    }

    public async Task<CategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await FindCategoryAsync(id, cancellationToken);

        return category is null ? null : CategoryResponse.From(category);
    }

    public async Task<bool> ChangeProductPriceAsync(
        Guid id,
        ChangeProductPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await FindProductForUpdateAsync(id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.ChangePrice(new Money(request.Price, request.Currency));

        await SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(
            request.Name,
            request.Description,
            new Money(request.Price, request.Currency),
            request.CategoryId);

        var category = await FindCategoryAsync(
            request.CategoryId,
            cancellationToken) ?? throw new InvalidProductCategoryException();

        await AddProductAsync(product, cancellationToken);

        await SaveChangesAsync(cancellationToken);

        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price.Amount,
            product.Price.Currency,
            product.CategoryId,
            category.Name,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt);
    }

    public async Task<bool> DiscontinueProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await FindProductForUpdateAsync(id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.Discontinue();

        await SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await FindProductForUpdateAsync(id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.UpdateDetails(
            request.Name,
            request.Description,
            new Money(request.Price, request.Currency),
            request.CategoryId);

        if (await FindCategoryAsync(request.CategoryId, cancellationToken)is null)
        {
            throw new InvalidProductCategoryException();
        }

        await SaveChangesAsync(cancellationToken);

        return true;
    }

    public Task<ProductResponse?> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default) => ReadProductAsync(
        id,
        cancellationToken);

    public Task<PagedResult<ProductResponse>> GetProductsAsync(
        ProductFilter query,
        CancellationToken cancellationToken = default)
    {
        query.Validate();

        return ReadProductsAsync(query, cancellationToken);
    }

    protected virtual Task<bool> CategoryNameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default) => db.Categories.AnyAsync(
        x => x.Name == name && (!excludingId.HasValue || x.Id != excludingId.Value),
        cancellationToken);

    protected virtual async Task<IReadOnlyList<Category>> ReadCategoriesAsync(
        CancellationToken cancellationToken = default) => await db.Categories.AsNoTracking().OrderBy(
        x => x.Name).ThenBy(
        x => x.Id).ToListAsync(cancellationToken);

    protected virtual Task<Category?> FindCategoryForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Categories.SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    protected virtual Task<bool> CategoryHasProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Products.AnyAsync(
        x => x.CategoryId == id,
        cancellationToken);

    protected virtual void RemoveCategory(Category category) => db.Categories.Remove(category);

    protected virtual Task<Category?> FindCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Categories.AsNoTracking().SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    protected virtual async Task AddCategoryAsync(
        Category category,
        CancellationToken cancellationToken = default) => await db.Categories.AddAsync(
        category,
        cancellationToken);

    protected virtual Task<Product?> FindProductForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Products.SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    protected virtual async Task AddProductAsync(
        Product product,
        CancellationToken cancellationToken = default) => await db.Products.AddAsync(
        product,
        cancellationToken);

    protected virtual Task<ProductResponse?> ReadProductAsync(
        Guid id,
        CancellationToken cancellationToken = default) => Project().SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    protected virtual async Task<PagedResult<ProductResponse>> ReadProductsAsync(
        ProductFilter query,
        CancellationToken cancellationToken = default)
    {
        query.Validate();

        var products = Filter(query);

        var count = await products.CountAsync(cancellationToken);

        var items = await products.OrderBy(
            x => x.Name).ThenBy(
            x => x.Id).Skip(
            (query.PageNumber - 1) * query.PageSize).Take(
            query.PageSize).ToListAsync(cancellationToken);

        return new(items, count, query.PageNumber, query.PageSize);
    }

    internal IQueryable<ProductResponse> Filter(ProductFilter query)
    {
        query.Validate();

        var products = Project();

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(x => x.CategoryId == categoryId);

        }

        var activeFilter = query.IsActive ?? (query.IncludeInactive ? (bool? )null : true);

        if (activeFilter is { } isActive)
        {
            products = products.Where(x => x.IsActive == isActive);

        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";

            products = products.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));

        }

        return products;
    }

    internal IQueryable<ProductResponse> Project() =>

        from product in db.Products.AsNoTracking()join category in db.Categories.AsNoTracking()on product.CategoryId equals category.Id

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

    private static string EscapeLike(
        string value) => value.Replace(
        "\\",
        "\\\\").Replace(
        "%",
        "\\%").Replace(
        "_",
        "\\_");

    protected virtual Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
