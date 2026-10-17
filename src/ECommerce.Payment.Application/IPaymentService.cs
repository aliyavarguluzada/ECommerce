using ECommerce.Payment.Domain;

namespace ECommerce.Payment.Application;

public interface IPaymentService
{
    Task<PaymentResponse> PayAsync(Guid customer, PayRequest request, CancellationToken ct);

    Task<PaymentResponse> GetAsync(Guid id, Guid customer, bool admin, CancellationToken ct);

    Task<PagedResult<PaymentResponse>> ListAsync(Guid? customer, int page, int size, CancellationToken ct);

    Task<PaymentResponse> ProcessAsync(Guid id, CancellationToken ct);

    Task<RefundSnapshot> RefundOrderAsync(Guid orderId, CancellationToken ct);

    Task RecoverAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(int limit, CancellationToken ct);
}
