using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Application.Common;
namespace ECommerce.Catalog.Application.Products.Queries.GetProducts;

public sealed class GetProductsHandler(IProductQueries queries)
{
    public Task<PagedResult<ProductResponse>> HandleAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
    {
        query.Validate();
        return queries.GetAllAsync(query, cancellationToken);
    }
}
