using ECommerce.Payment.Domain;

namespace ECommerce.Payment.Application;

public sealed record PayCommand(Guid RequestId, Guid OrderId, string MethodToken);

public sealed record OrderSnapshot(Guid Id, Guid CustomerId, string Status, string Currency, decimal Total);

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Provider,
    string? ProviderReference,
    string? FailureReason,
    bool OrderNotified,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static PaymentResponse From(
        PaymentRecord payment) => new(
        payment.Id,
        payment.OrderId,
        payment.CustomerId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        "Development (no real charge)",
        payment.ProviderReference,
        payment.FailureReason,
        payment.OrderNotified,
        payment.CreatedAt,
        payment.UpdatedAt);

}

public sealed record RefundSnapshot(Guid PaymentId, string Status);

public sealed record ProviderResult(bool Succeeded, string? Reference, string? Failure);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

public interface IPaymentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);

}

public interface IPaymentStore
{
    Task<IPaymentTransaction> BeginAsync(Guid orderId, CancellationToken ct);

    Task<PaymentRecord?> FindAsync(Guid id, CancellationToken ct);

    Task<PaymentRecord?> FindActiveByOrderAsync(Guid orderId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(int limit, CancellationToken ct);

    Task<PagedResult<PaymentResponse>> ListAsync(Guid? customerId, int page, int size, CancellationToken ct);

    void Add(PaymentRecord payment);

    Task SaveAsync(CancellationToken ct);

}

public interface IOrderClient
{
    Task<OrderSnapshot> GetAsync(Guid orderId, CancellationToken ct);

    Task RecordPaidAsync(Guid orderId, Guid paymentId, decimal amount, string currency, CancellationToken ct);

}

public interface IPaymentProvider
{
    Task<ProviderResult> ChargeAsync(
        Guid requestId,
        decimal amount,
        string currency,
        string methodToken,
        CancellationToken ct);

    Task RefundAsync(Guid requestId, string providerReference, decimal amount, string currency, CancellationToken ct);

}
