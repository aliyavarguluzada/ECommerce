using ECommerce.Order.Domain;

namespace ECommerce.Order.Application;

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

}

public interface ICatalogClient
{
    Task<CatalogProduct> GetAsync(Guid productId, CancellationToken cancellationToken);

}

public interface IInventoryClient
{
    Task<ReservationSnapshot> ReserveAsync(
        Guid orderId,
        IEnumerable<OrderItemRequest> items,
        CancellationToken cancellationToken);

    Task<ReservationSnapshot> CommitAsync(Guid orderId, CancellationToken cancellationToken);

    Task<ReservationSnapshot> ReleaseAsync(Guid orderId, CancellationToken cancellationToken);

}

public interface IPaymentClient
{
    Task<RefundSnapshot> RefundAsync(Guid orderId, CancellationToken cancellationToken);

}
