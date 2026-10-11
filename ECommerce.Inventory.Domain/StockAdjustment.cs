namespace ECommerce.Inventory.Domain;

public sealed class StockAdjustment
{
    public Guid RequestId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Delta { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    private StockAdjustment() { }
    public static StockAdjustment Create(Guid requestId, Guid productId, int delta, string reason)
    {
        if (requestId == Guid.Empty || productId == Guid.Empty || delta == 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new DomainException("Request ID, product ID, a non-zero adjustment, and a reason of at most 500 characters are required.");
        return new() { RequestId = requestId, ProductId = productId, Delta = delta, Reason = reason.Trim(), CreatedAt = DateTime.UtcNow };
    }
    public bool Matches(StockAdjustment other) => ProductId == other.ProductId && Delta == other.Delta && Reason == other.Reason;
}
