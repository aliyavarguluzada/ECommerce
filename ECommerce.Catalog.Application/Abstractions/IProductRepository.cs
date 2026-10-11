using ECommerce.Catalog.Domain.Aggregates.Products;
namespace ECommerce.Catalog.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
}
