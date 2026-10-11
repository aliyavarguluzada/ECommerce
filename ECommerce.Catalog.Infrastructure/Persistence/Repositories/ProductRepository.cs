using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Catalog.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await db.Products.AddAsync(product, cancellationToken);
}
