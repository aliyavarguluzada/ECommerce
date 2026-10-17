using ECommerce.Inventory.Domain;

namespace ECommerce.Inventory.Application;

public interface IInventoryService
{
    Task<StockResponse> CreateAsync(CreateStockRequest request, CancellationToken ct);

    Task<StockResponse> GetAsync(
        Guid id,
        CancellationToken ct);

    Task<PagedResult<StockResponse>> ListAsync(int page, int size, int? maxAvailable, CancellationToken ct);

    Task<PagedResult<AdjustmentResponse>> AdjustmentsAsync(
        Guid productId,
        int page,
        int size,
        CancellationToken ct);

    Task<StockResponse> AdjustAsync(Guid productId, AdjustStockRequest request, CancellationToken ct);

    Task<ReservationResponse> GetReservationAsync(
        Guid id,
        CancellationToken ct);

    Task<PagedResult<ReservationResponse>> ReservationsAsync(
        int page,
        int size,
        ReservationStatus? status,
        CancellationToken ct);

    Task<ReservationResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct);

    Task<ReservationResponse> CommitAsync(Guid id, CancellationToken ct);

    Task<ReservationResponse> ReleaseAsync(Guid id, CancellationToken ct);

}
