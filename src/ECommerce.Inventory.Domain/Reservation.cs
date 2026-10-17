namespace ECommerce.Inventory.Domain;

public enum ReservationStatus
{
    Reserved,

    Committed,

    Released

}

public sealed class Reservation
{
    private readonly List<ReservationLine> _items = [];

    public Guid OrderId { get; private set; }

    public ReservationStatus Status { get; private set; }

    public IReadOnlyCollection<ReservationLine> Items => _items.AsReadOnly();

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private Reservation()
    {
    }

    public static Reservation Create(Guid orderId, IEnumerable<ReservationLine> items)
    {
        var lines = items.ToArray();

        if (orderId == Guid.Empty || lines.Length is < 1 or > 100 || lines.Select(
            x => x.ProductId).Distinct().Count() != lines.Length)
        {
            throw new DomainException("A reservation needs an order ID and 1–100 distinct products.");
        }

        var result = new Reservation
        {
            OrderId = orderId,

            Status = ReservationStatus.Reserved,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow

        };

        result._items.AddRange(lines.OrderBy(x => x.ProductId));

        return result;
    }

    // Prevent a delayed reserve request from recreating stock holds after cancellation.

    public static Reservation ReleasedTombstone(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order ID is required.");
        }

        return new()
        {
            OrderId = orderId,

            Status = ReservationStatus.Released,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow

        };
    }

    public bool Matches(
        IEnumerable<ReservationLine> items) => _items.OrderBy(
        x => x.ProductId).SequenceEqual(
        items.OrderBy(x => x.ProductId));

    public void Commit()
    {
        if (Status == ReservationStatus.Released)
        {
            throw new DomainException("Released reservations cannot be committed.");
        }

        Status = ReservationStatus.Committed;

        UpdatedAt = DateTime.UtcNow;

    }

    public void Release()
    {
        Status = ReservationStatus.Released;

        UpdatedAt = DateTime.UtcNow;

    }

}

public sealed record ReservationLine
{
    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    private ReservationLine()
    {
    }

    public ReservationLine(Guid productId, int quantity)
    {
        if (productId == Guid.Empty || quantity is < 1 or > 100000)
        {
            throw new DomainException("Reservation quantity must be 1–100000.");
        }

        ProductId = productId;

        Quantity = quantity;

    }

}
