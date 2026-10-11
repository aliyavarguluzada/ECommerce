using ECommerce.Payment.Application;
namespace ECommerce.Payment.Infrastructure;
public sealed class DevelopmentPaymentProvider(bool enabled) : IPaymentProvider
{
    public Task<ProviderResult> ChargeAsync(Guid id, decimal amount, string currency, string token, CancellationToken ct)
    {
        if (!enabled) throw new ServiceException(503, "A production payment provider is not configured.");
        return Task.FromResult(token == "dev-approved" ? new ProviderResult(true, "dev-charge-" + id, null)
            : new ProviderResult(false, null, "Development provider declined the payment."));
    }
    public Task RefundAsync(Guid id, string reference, decimal amount, string currency, CancellationToken ct)
    {
        if (!enabled) throw new ServiceException(503, "A production payment provider is not configured.");
        return Task.CompletedTask;
    }
}
