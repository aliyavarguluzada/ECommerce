namespace ECommerce.Cart.Domain;
public sealed class ShoppingCart
{
    private readonly List<CartLine> _items = [];
    public Guid CustomerId { get; private set; }
    public Guid? PendingCheckoutId { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<CartLine> Items => _items.AsReadOnly();
    private ShoppingCart() { }
    public static ShoppingCart Create(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new DomainException("Customer ID is required.");
        return new() { CustomerId = customerId, UpdatedAt = DateTime.UtcNow };
    }
    public void SetQuantity(Guid productId, int quantity)
    {
        Editable();
        if (productId == Guid.Empty || quantity is < 1 or > 100000) throw new DomainException("Product and quantity 1–100000 are required.");
        var line = _items.SingleOrDefault(x => x.ProductId == productId);
        if (line is null)
        {
            if (_items.Count >= 100) throw new DomainException("A cart supports at most 100 distinct products.");
            _items.Add(new(productId, quantity));
        }
        else line.SetQuantity(quantity);
        UpdatedAt = DateTime.UtcNow;
    }
    public void Remove(Guid productId) { Editable(); _items.RemoveAll(x => x.ProductId == productId); UpdatedAt = DateTime.UtcNow; }
    public void Clear() { Editable(); _items.Clear(); UpdatedAt = DateTime.UtcNow; }
    public void BeginCheckout(Guid id)
    {
        Editable();
        if (id == Guid.Empty || _items.Count == 0) throw new DomainException("Checkout requires a request ID and a non-empty cart.");
        PendingCheckoutId = id; UpdatedAt = DateTime.UtcNow;
    }
    public void CompleteCheckout(Guid id)
    {
        if (PendingCheckoutId != id) throw new DomainException("Checkout does not match the pending cart.");
        _items.Clear(); PendingCheckoutId = null; UpdatedAt = DateTime.UtcNow;
    }
    public void AbandonCheckout(Guid id) { if (PendingCheckoutId == id) { PendingCheckoutId = null; UpdatedAt = DateTime.UtcNow; } }
    private void Editable() { if (PendingCheckoutId.HasValue) throw new DomainConflictException("Checkout is pending. Retry its original request before editing the cart."); }
}
public sealed class CartLine
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    private CartLine() { }
    public CartLine(Guid productId, int quantity)
    {
        if (productId == Guid.Empty) throw new DomainException("Product ID is required.");
        ProductId = productId; SetQuantity(quantity);
    }
    public void SetQuantity(int quantity)
    {
        if (quantity is < 1 or > 100000) throw new DomainException("Quantity must be 1–100000.");
        Quantity = quantity;
    }
}
public enum CheckoutStatus { Pending, Completed, Failed }
public sealed class CheckoutAttempt
{
    private readonly List<CartLine> _items = [];
    public Guid RequestId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string AddressJson { get; private set; } = null!;
    public string AddressHash { get; private set; } = null!;
    public CheckoutStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<CartLine> Items => _items.AsReadOnly();
    private CheckoutAttempt() { }
    public static CheckoutAttempt Create(Guid id, Guid customer, string json, string hash, IEnumerable<CartLine> items)
    {
        var snapshot = items.ToArray();
        if (id == Guid.Empty || customer == Guid.Empty || string.IsNullOrWhiteSpace(json) || hash.Length != 64 ||
            snapshot.Length is < 1 or > 100 || snapshot.Select(x => x.ProductId).Distinct().Count() != snapshot.Length)
            throw new DomainException("Invalid checkout snapshot.");
        var attempt = new CheckoutAttempt { RequestId = id, CustomerId = customer, AddressJson = json,
            AddressHash = hash, CreatedAt = DateTime.UtcNow, Status = CheckoutStatus.Pending };
        attempt._items.AddRange(snapshot.Select(x => new CartLine(x.ProductId, x.Quantity))); return attempt;
    }
    public void Complete() => Status = CheckoutStatus.Completed;
    public void Fail() => Status = CheckoutStatus.Failed;
}
