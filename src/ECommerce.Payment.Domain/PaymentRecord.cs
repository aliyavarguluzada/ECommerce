namespace ECommerce.Payment.Domain;

public enum PaymentStatus
{
    Pending,

    Succeeded,

    Failed,

    Refunding,

    Refunded

}

public sealed class PaymentRecord
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = null!;

    public string MethodToken { get; private set; } = null!;

    public PaymentStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureReason { get; private set; }

    public bool OrderNotified { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? LastRecoveryAttemptAt { get; private set; }

    private PaymentRecord()
    {
    }

    public static PaymentRecord Create(
        Guid id,
        Guid order,
        Guid customer,
        decimal amount,
        string currency,
        string method)
    {
        if (id == Guid.Empty || order == Guid.Empty || customer == Guid.Empty || amount < 0 || amount > 9999999999999999.99m || decimal.Round(
            amount,
            2) != amount || string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency.Any(
            c => c is < 'A' or > 'Z') || method is not ("dev-approved" or "dev-declined"))
        {
            throw new DomainException("Use an order, request ID, and development method token dev-approved or dev-declined.");

        }

        return new()
        {
            Id = id,

            OrderId = order,

            CustomerId = customer,

            Amount = amount,

            Currency = currency,

            MethodToken = method,

            Status = PaymentStatus.Pending,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow

        };
    }

    public void Succeed(string reference)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException("Payment is not pending.");

        }

        ProviderReference = reference;

        Status = PaymentStatus.Succeeded;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Fail(string reason)
    {
        Status = PaymentStatus.Failed;

        FailureReason = reason;

        UpdatedAt = DateTime.UtcNow;

    }

    public void BeginRefund()
    {
        if (Status != PaymentStatus.Succeeded)
        {
            throw new DomainException("Only successful payments can be refunded.");

        }

        Status = PaymentStatus.Refunding;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Refund()
    {
        if (Status != PaymentStatus.Refunding)
        {
            throw new DomainException("Refund is not pending.");

        }

        Status = PaymentStatus.Refunded;

        UpdatedAt = DateTime.UtcNow;

    }

    public void MarkNotified()
    {
        OrderNotified = true;

        UpdatedAt = DateTime.UtcNow;

    }

    public void RecordRecoveryAttempt() => LastRecoveryAttemptAt = DateTime.UtcNow;

}
