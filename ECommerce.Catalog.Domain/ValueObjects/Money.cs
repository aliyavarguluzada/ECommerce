using ECommerce.Catalog.Domain.Exceptions;

namespace ECommerce.Catalog.Domain.ValueObjects;

// Catalog currently supports amounts with at most two decimal places.
public sealed record Money
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;

    private Money() { }

    public Money(decimal amount, string currency)
    {
        if (amount < 0 || amount > 9999999999999999.99m)
            throw new DomainException("Amount must be non-negative and fit decimal(18, 2).");

        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("Amount must have at most two decimal places.");

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (normalizedCurrency is null || normalizedCurrency.Length != 3 ||
            normalizedCurrency.Any(c => c is < 'A' or > 'Z'))
            throw new DomainException("Currency must contain three letters.");

        Amount = amount;
        Currency = normalizedCurrency;
    }
}
