namespace ECommerce.Order.Domain;

public enum OrderStatus { PendingInventory, Reserved, Confirming, Confirmed, Cancelling, Cancelled, Shipped, Delivered }
public sealed class PurchaseOrder
{
    private readonly List<OrderLine> _items = [];
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; } = null!;
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    public IReadOnlyCollection<OrderLine> Items => _items.AsReadOnly();
    public decimal Total { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? Carrier { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastRecoveryAttemptAt { get; private set; }
    public Guid? PaymentId { get; private set; }
    public bool PaymentRefunded { get; private set; }
    private PurchaseOrder() { }
    public static PurchaseOrder Create(Guid id, Guid customerId, string fingerprint, string currency,
        ShippingAddress address, IEnumerable<OrderLine> items)
    {
        var lines = items.ToArray();
        if (id == Guid.Empty || customerId == Guid.Empty || fingerprint.Length != 64 || lines.Length is < 1 or > 100 ||
            lines.Select(x => x.ProductId).Distinct().Count() != lines.Length) throw new DomainException("Order identity, customer, and distinct items are required.");
        var code = currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(c => c is < 'A' or > 'Z')) throw new DomainException("Invalid currency.");
        ArgumentNullException.ThrowIfNull(address);
        var total = lines.Sum(x => x.LineTotal);
        if (total > 9999999999999999.99m) throw new DomainException("Order total exceeds supported amount.");
        var order = new PurchaseOrder { Id = id, CustomerId = customerId, RequestFingerprint = fingerprint,
            Currency = code, ShippingAddress = address, Total = total, Status = OrderStatus.PendingInventory,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        order._items.AddRange(lines.OrderBy(x => x.ProductId)); return order;
    }
    public void MarkReserved() { Require(OrderStatus.PendingInventory); Set(OrderStatus.Reserved); }
    public void BeginConfirmation()
    {
        Require(OrderStatus.Reserved);
        if (!PaymentId.HasValue || PaymentRefunded) throw new DomainConflictException("Successful payment is required before confirmation.");
        Set(OrderStatus.Confirming);
    }
    public void RecordPayment(Guid paymentId, decimal amount, string currency)
    {
        if (paymentId == Guid.Empty || amount != Total || currency != Currency)
            throw new DomainConflictException("Payment does not match the order total and currency.");
        if (Status is OrderStatus.Cancelled or OrderStatus.Cancelling || PaymentRefunded)
            throw new DomainConflictException("Cancelled or refunded orders cannot accept payment.");
        if (PaymentId.HasValue && PaymentId != paymentId) throw new DomainConflictException("Order already has another payment.");
        PaymentId = paymentId; UpdatedAt = DateTime.UtcNow;
    }
    public void RecordRefund() { PaymentRefunded = true; UpdatedAt = DateTime.UtcNow; }
    public void Confirm() { Require(OrderStatus.Confirming); Set(OrderStatus.Confirmed); }
    public void BeginCancellation(string reason)
    {
        if (Status is not (OrderStatus.PendingInventory or OrderStatus.Reserved or OrderStatus.Confirmed))
            throw new DomainConflictException("Only pending, reserved, or confirmed orders may be cancelled.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new DomainException("Cancellation reason must contain 1–500 characters.");
        CancellationReason = reason.Trim(); Set(OrderStatus.Cancelling);
    }
    public void Cancel() { Require(OrderStatus.Cancelling); Set(OrderStatus.Cancelled); }
    public void Ship(string carrier, string trackingNumber)
    {
        Require(OrderStatus.Confirmed);
        if (!PaymentId.HasValue || PaymentRefunded) throw new DomainConflictException("A paid order is required for shipping.");
        if (string.IsNullOrWhiteSpace(carrier) || carrier.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Trim().Length > 150)
            throw new DomainException("Carrier and tracking number are required.");
        Carrier = carrier.Trim(); TrackingNumber = trackingNumber.Trim(); Set(OrderStatus.Shipped);
    }
    public void Deliver() { Require(OrderStatus.Shipped); Set(OrderStatus.Delivered); }
    public void RecordRecoveryAttempt() => LastRecoveryAttemptAt = DateTime.UtcNow;
    private void Set(OrderStatus state) { Status = state; UpdatedAt = DateTime.UtcNow; }
    private void Require(OrderStatus expected)
    {
        if (Status != expected) throw new DomainConflictException($"Order must be {expected}; current state is {Status}.");
    }
}
