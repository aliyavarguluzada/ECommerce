using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Common;

namespace ECommerce.Catalog.Application;

public interface ICatalogService
{
    Task<CategoryResponse> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ChangeProductPriceAsync(
        Guid id,
        ChangeProductPriceRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DiscontinueProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ProductResponse>> GetProductsAsync(
        ProductFilter query,
        CancellationToken cancellationToken = default);

}
