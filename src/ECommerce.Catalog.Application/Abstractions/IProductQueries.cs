using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Products.Queries.GetProducts;

namespace ECommerce.Catalog.Application.Abstractions;

public interface IProductQueries
{
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductResponse>> GetAllAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken = default);

}
