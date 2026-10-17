using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Products.Queries.GetProductById;

public sealed class GetProductByIdHandler(IProductQueries queries)
{
    public Task<ProductResponse?> HandleAsync(
        Guid id,
        CancellationToken cancellationToken = default) => queries.GetByIdAsync(
        id,
        cancellationToken);

}
