using Inv = ECommerce.Inventory.Application;
using Ord = ECommerce.Order.Application;
using Car = ECommerce.Cart.Application;
using Pay = ECommerce.Payment.Application;
using ECommerce.Inventory.Domain;
namespace ECommerce.Tests.Support;

public sealed record CatalogEntry(Guid Id, string Name, decimal Price, string Currency, bool Active);
public sealed class CommerceWorld
{
    public Dictionary<Guid, CatalogEntry> Products { get; } = [];
    public InventoryStore InventoryStore { get; } = new();
    public OrderStore OrderStore { get; } = new();
    public CartStore CartStore { get; } = new();
    public PaymentStore PaymentStore { get; } = new();
    public ProviderSpy Provider { get; } = new();
    public CatalogClients Catalog { get; }
    public InventoryAdapter InventoryClient { get; }
    public PaymentOrderAdapter PaymentOrderClient { get; }
    public CartOrderAdapter CartOrderClient { get; }
    public Inv.InventoryService Inventory { get; }
    public Ord.OrderService Order { get; }
    public Car.CartService Cart { get; }
    public Pay.PaymentService Payment { get; }
    public CommerceWorld()
    {
        Catalog = new(Products); Inventory = new(InventoryStore, Catalog);
        InventoryClient = new(Inventory);
        var refunds = new RefundAdapter();
        Order = new(OrderStore, Catalog, InventoryClient, refunds);
        PaymentOrderClient = new(Order); Payment = new(PaymentStore, PaymentOrderClient, Provider);
        refunds.Payment = Payment;
        CartOrderClient = new(Order); Cart = new(CartStore, Catalog, CartOrderClient);
    }
    public Guid AddProduct(string name = "Mouse", decimal price = 10m, string currency = "USD", int stock = 100, bool active = true)
    {
        var id = Guid.NewGuid(); Products.Add(id, new(id, name, price, currency, active));
        InventoryStore.Data.Stocks.Add(id, StockItem.Create(id, stock)); return id;
    }
    public static Ord.AddressRequest Address() => new("Customer", "1 Street", null, "Baku", null, "AZ1000", "AZ");
    public static Car.AddressRequest CartAddress() => new("Customer", "1 Street", null, "Baku", null, "AZ1000", "AZ");
    public Ord.CreateOrderCommand NewOrder(Guid product, Guid? customer = null, int quantity = 2) =>
        new(Guid.NewGuid(), customer ?? TestCredentials.Customer, Address(), [new(product, quantity)]);
}
public sealed class CatalogClients(Dictionary<Guid, CatalogEntry> products) : Inv.ICatalogClient, Ord.ICatalogClient, Car.ICatalogClient
{
    public bool Unavailable { get; set; }
    private CatalogEntry Get(Guid id)
    {
        if (!products.TryGetValue(id, out var product)) throw new KeyNotFoundException();
        return product;
    }
    Task<Inv.CatalogProduct> Inv.ICatalogClient.GetAsync(Guid id, CancellationToken ct)
    {
        if (Unavailable) throw new Inv.ServiceException(503, "Catalog down.");
        try { var p = Get(id); return Task.FromResult(new Inv.CatalogProduct(p.Id, p.Name, p.Price, p.Currency, p.Active)); }
        catch (KeyNotFoundException) { throw new Inv.ServiceException(400, "Product missing."); }
    }
    Task<Ord.CatalogProduct> Ord.ICatalogClient.GetAsync(Guid id, CancellationToken ct)
    {
        if (Unavailable) throw new Ord.ServiceException(503, "Catalog down.");
        try { var p = Get(id); return Task.FromResult(new Ord.CatalogProduct(p.Id, p.Name, p.Price, p.Currency, p.Active)); }
        catch (KeyNotFoundException) { throw new Ord.ServiceException(400, "Product missing."); }
    }
    Task<Car.CatalogProduct?> Car.ICatalogClient.GetAsync(Guid id, CancellationToken ct)
    {
        if (Unavailable) throw new Car.ServiceException(503, "Catalog down.");
        return Task.FromResult(products.TryGetValue(id, out var p) ? new Car.CatalogProduct(p.Id, p.Name, p.Price, p.Currency, p.Active) : null);
    }
}
public sealed class InventoryAdapter(Inv.InventoryService inventory) : Ord.IInventoryClient
{
    public bool Unavailable { get; set; }
    public bool LoseNextReserveResponse { get; set; }
    public async Task<Ord.ReservationSnapshot> ReserveAsync(Guid id, IEnumerable<Ord.OrderItemRequest> items, CancellationToken ct)
    {
        if (Unavailable) throw new Ord.ServiceException(503, "Inventory down.");
        try
        {
            var response = await inventory.ReserveAsync(new(id, items.Select(x => new Inv.ReserveLineRequest(x.ProductId, x.Quantity)).ToArray()), ct);
            if (LoseNextReserveResponse) { LoseNextReserveResponse = false; throw new Ord.ServiceException(503, "Response lost after reserve."); }
            return new(id, response.Status.ToString());
        }
        catch (Inv.ServiceException error) { throw new Ord.ServiceException(error.StatusCode, error.Message); }
    }
    public async Task<Ord.ReservationSnapshot> CommitAsync(Guid id, CancellationToken ct)
    {
        if (Unavailable) throw new Ord.ServiceException(503, "Inventory down.");
        var response = await inventory.CommitAsync(id, ct); return new(id, response.Status.ToString());
    }
    public async Task<Ord.ReservationSnapshot> ReleaseAsync(Guid id, CancellationToken ct)
    {
        if (Unavailable) throw new Ord.ServiceException(503, "Inventory down.");
        var response = await inventory.ReleaseAsync(id, ct); return new(id, response.Status.ToString());
    }
}
public sealed class RefundAdapter : Ord.IPaymentClient
{
    public Pay.PaymentService Payment { get; set; } = null!;
    public async Task<Ord.RefundSnapshot> RefundAsync(Guid id, CancellationToken ct)
    {
        try { var refund = await Payment.RefundOrderAsync(id, ct); return new(refund.PaymentId, refund.Status); }
        catch (Pay.ServiceException error) { throw new Ord.ServiceException(error.StatusCode == 404 ? 503 : error.StatusCode, error.Message); }
    }
}
public sealed class PaymentOrderAdapter(Ord.OrderService orders) : Pay.IOrderClient
{
    public bool NotificationUnavailable { get; set; }
    public bool LoseNextNotificationResponse { get; set; }
    public async Task<Pay.OrderSnapshot> GetAsync(Guid id, CancellationToken ct)
    { var order = await orders.GetAsync(id, ct); return new(order.Id, order.CustomerId, order.Status.ToString(), order.Currency, order.Total); }
    public async Task RecordPaidAsync(Guid id, Guid payment, decimal amount, string currency, CancellationToken ct)
    {
        if (NotificationUnavailable) throw new Pay.ServiceException(503, "Callback down.");
        try
        {
            await orders.RecordPaymentAsync(id, new(payment, amount, currency), ct);
            if (LoseNextNotificationResponse) { LoseNextNotificationResponse = false; throw new Pay.ServiceException(503, "Callback response lost."); }
        }
        catch (Ord.ServiceException error) { throw new Pay.ServiceException(error.StatusCode, error.Message); }
        catch (ECommerce.Order.Domain.DomainConflictException error) { throw new Pay.ServiceException(409, error.Message); }
    }
}
public sealed class CartOrderAdapter(Ord.OrderService orders) : Car.IOrderClient
{
    public bool LoseNextCreateResponse { get; set; }
    public bool RejectCreate { get; set; }
    public async Task<Car.OrderSnapshot> CreateAsync(Guid id, Guid customer, Car.AddressRequest address, IEnumerable<ECommerce.Cart.Domain.CartLine> items, CancellationToken ct)
    {
        if (RejectCreate) throw new Car.ServiceException(400, "Order rejected.");
        var order = await orders.CreateAsync(new(id, customer, new(address.Recipient, address.Line1, address.Line2, address.City, address.Region, address.PostalCode, address.CountryCode),
            items.Select(x => new Ord.OrderItemRequest(x.ProductId, x.Quantity)).ToArray()), ct);
        if (LoseNextCreateResponse) { LoseNextCreateResponse = false; throw new Car.ServiceException(503, "Checkout response lost."); }
        return new(order.Id, order.CustomerId, order.Status.ToString(), order.Currency, order.Total);
    }
    public async Task<Car.OrderSnapshot> GetAsync(Guid id, CancellationToken ct)
    { var order = await orders.GetAsync(id, ct); return new(order.Id, order.CustomerId, order.Status.ToString(), order.Currency, order.Total); }
}
public sealed class ProviderSpy : Pay.IPaymentProvider
{
    private readonly Dictionary<Guid, Pay.ProviderResult> _charges = [];
    private readonly HashSet<Guid> _refunds = [];
    public bool Unavailable { get; set; }
    public int ChargeCount => _charges.Count;
    public int RefundCount => _refunds.Count;
    public Task<Pay.ProviderResult> ChargeAsync(Guid id, decimal amount, string currency, string method, CancellationToken ct)
    {
        if (Unavailable) throw new Pay.ServiceException(503, "Provider down.");
        if (!_charges.TryGetValue(id, out var result)) _charges[id] = result = new(method == "dev-approved", "charge-" + id, method == "dev-approved" ? null : "Declined");
        return Task.FromResult(result);
    }
    public Task RefundAsync(Guid id, string reference, decimal amount, string currency, CancellationToken ct)
    { if (Unavailable) throw new Pay.ServiceException(503, "Refund down."); _refunds.Add(id); return Task.CompletedTask; }
}
