using ECommerce.Inventory.Domain;
namespace ECommerce.Inventory.Application;

public interface ITransaction : IAsyncDisposable { Task CommitAsync(CancellationToken cancellationToken); }
public interface IInventoryStore
{
    Task<ITransaction> BeginAsync(Guid operationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockItem>> LockStocksAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken);
    Task<StockItem?> FindStockAsync(Guid productId, CancellationToken cancellationToken);
    Task<Reservation?> FindReservationAsync(Guid orderId, CancellationToken cancellationToken);
    Task<StockAdjustment?> FindAdjustmentAsync(Guid requestId, CancellationToken cancellationToken);
    Task<PagedResult<StockResponse>> GetStocksAsync(int page, int size, int? maxAvailable, CancellationToken cancellationToken);
    Task<PagedResult<AdjustmentResponse>> GetAdjustmentsAsync(Guid productId, int page, int size, CancellationToken cancellationToken);
    Task<PagedResult<ReservationResponse>> GetReservationsAsync(int page, int size, ReservationStatus? status, CancellationToken cancellationToken);
    void Add(StockItem stock);
    void Add(Reservation reservation);
    void Add(StockAdjustment adjustment);
    Task SaveAsync(CancellationToken cancellationToken);
}
public interface ICatalogClient { Task<CatalogProduct> GetAsync(Guid productId, CancellationToken cancellationToken); }
