namespace ECommerce.Inventory.Domain;

public sealed class StockItem
{
    public Guid ProductId { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int Available => OnHand - Reserved;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private StockItem()
    {
    }

    public static StockItem Create(Guid productId, int onHand)
    {
        if (productId == Guid.Empty || onHand < 0)
        {
            throw new DomainException("Product ID and non-negative initial stock are required.");

        }

        return new()
        {
            ProductId = productId,

            OnHand = onHand,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow

        };
    }

    public void Adjust(int delta)
    {
        var amount = (long)OnHand + delta;

        if (delta == 0 || amount < Reserved || amount > int.MaxValue)
        {
            throw new DomainException("Adjustment must be non-zero and cannot reduce stock below reserved units or overflow.");

        }

        OnHand = (int)amount;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Reserve(int quantity)
    {
        Positive(quantity);

        if (quantity > Available)
        {
            throw new DomainException("Insufficient available stock.");

        }

        Reserved += quantity;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Commit(int quantity)
    {
        Positive(quantity);

        if (quantity > Reserved)
        {
            throw new DomainException("Reservation exceeds reserved stock.");

        }

        Reserved -= quantity;

        OnHand -= quantity;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Release(int quantity)
    {
        Positive(quantity);

        if (quantity > Reserved)
        {
            throw new DomainException("Reservation exceeds reserved stock.");

        }

        Reserved -= quantity;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Restore(int quantity)
    {
        Positive(quantity);

        Adjust(quantity);

    }

    private static void Positive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.");

        }

    }

}
