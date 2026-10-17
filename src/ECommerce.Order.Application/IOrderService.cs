using ECommerce.Order.Domain;

namespace ECommerce.Order.Application;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct);

    Task<OrderResponse> GetAsync(
        Guid id,
        CancellationToken ct);

    Task<PagedResult<OrderResponse>> ListAsync(
        int page,
        int size,
        Guid? customerId,
        OrderStatus? status,
        CancellationToken ct);

    Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken ct);

    Task<OrderResponse> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken ct);

    Task<OrderResponse> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct);

    Task<OrderResponse> RetryAsync(Guid id, CancellationToken ct);

    Task<OrderResponse> ShipAsync(Guid id, ShipOrderRequest request, CancellationToken ct);

    Task<OrderResponse> RecoverAsync(Guid id, DateTime expiryCutoff, CancellationToken ct);

    Task<OrderResponse> DeliverAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(DateTime cutoff, int limit, CancellationToken ct);
}
