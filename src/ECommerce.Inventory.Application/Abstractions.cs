using ECommerce.Inventory.Domain;

namespace ECommerce.Inventory.Application;

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

}

public interface ICatalogClient
{
    Task<CatalogProduct> GetAsync(Guid productId, CancellationToken cancellationToken);

}
