using Inv = ECommerce.Inventory.Application;
using Ord = ECommerce.Order.Application;
using Car = ECommerce.Cart.Application;
using Pay = ECommerce.Payment.Application;
using ECommerce.Inventory.Domain;
using ECommerce.Order.Domain;
using ECommerce.Cart.Domain;
using ECommerce.Payment.Domain;

namespace ECommerce.Tests.Support;

public sealed class InventoryData
{
    public Dictionary<Guid, StockItem> Stocks = [];

    public Dictionary<Guid, Reservation> Reservations = [];

    public Dictionary<Guid, StockAdjustment> Adjustments = [];

}

public sealed class InventoryTestService(Inv.ICatalogClient catalog, TransactionalState<InventoryData>? initialState = null) : ECommerce.Inventory.Infrastructure.InventoryService(null!, catalog)
{
    public TransactionalState<InventoryData> State { get; } = initialState ?? new();
    public InventoryData Data => State.Data;
    private InventoryData Current => State.Current;

    protected override Task<Inv.ITransaction> BeginTransactionAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Inv.ITransaction>(
        new Transaction(State.Start()));

    protected override Task<IReadOnlyList<StockItem>> LockStocksAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<StockItem>>(
        ids.Distinct().Order().Where(Current.Stocks.ContainsKey).Select(x => Current.Stocks[x]).ToArray());

    protected override Task<StockItem?> FindStockAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Stocks.GetValueOrDefault(id));

    protected override Task<Reservation?> FindReservationAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Reservations.GetValueOrDefault(id));

    protected override Task<StockAdjustment?> FindAdjustmentAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Adjustments.GetValueOrDefault(id));

    protected override Task<Inv.PagedResult<Inv.StockResponse>> ReadStocksAsync(int page, int size, int? max, CancellationToken ct)
    {
        var rows = Current.Stocks.Values.Where(
            x => !max.HasValue || x.Available <= max).OrderBy(
            x => x.ProductId).ToArray();

        return Task.FromResult(
            new Inv.PagedResult<Inv.StockResponse>(
                rows.Skip((page - 1) * size).Take(size).Select(Inv.StockResponse.From).ToArray(),
                rows.Length,
                page,
                size));
    }

    protected override Task<Inv.PagedResult<Inv.AdjustmentResponse>> ReadAdjustmentsAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct)
    {
        var rows = Current.Adjustments.Values.Where(
            x => x.ProductId == id).OrderByDescending(
            x => x.CreatedAt).ToArray();

        return Task.FromResult(
            new Inv.PagedResult<Inv.AdjustmentResponse>(
                rows.Skip(
                    (page - 1) * size).Take(size).Select(
                    x => new Inv.AdjustmentResponse(
                        x.RequestId,
                        x.ProductId,
                        x.Delta,
                        x.Reason,
                        x.CreatedAt)).ToArray(),
                rows.Length,
                page,
                size));
    }

    protected override Task<Inv.PagedResult<Inv.ReservationResponse>> ReadReservationsAsync(
        int page,
        int size,
        ReservationStatus? status,
        CancellationToken ct)
    {
        var rows = Current.Reservations.Values.Where(x => !status.HasValue || x.Status == status).ToArray();

        return Task.FromResult(
            new Inv.PagedResult<Inv.ReservationResponse>(
                rows.Skip((page - 1) * size).Take(size).Select(Inv.ReservationResponse.From).ToArray(),
                rows.Length,
                page,
                size));
    }

    protected override void Add(StockItem stock) => Current.Stocks.Add(stock.ProductId, stock);

    protected override void Add(Reservation reservation) => Current.Reservations.Add(reservation.OrderId, reservation);

    protected override void Add(StockAdjustment adjustment) => Current.Adjustments.Add(adjustment.RequestId, adjustment);

    protected override Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(TransactionalState<InventoryData>.Lease lease) : Inv.ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}

public sealed class OrderData
{
    public Dictionary<Guid, PurchaseOrder> Orders = [];

}

public sealed class OrderTestService(Ord.ICatalogClient catalog, Ord.IInventoryClient inventory, Ord.IPaymentClient payments, TransactionalState<OrderData>? initialState = null) : ECommerce.Order.Infrastructure.OrderService(null!, catalog, inventory, payments)
{
    public TransactionalState<OrderData> State { get; } = initialState ?? new();
    public OrderData Data => State.Data;
    private OrderData Current => State.Current;

    protected override Task<Ord.ITransaction> BeginTransactionAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Ord.ITransaction>(
        new Transaction(State.Start()));

    protected override Task<PurchaseOrder?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Orders.GetValueOrDefault(id));

    protected override Task<Ord.PagedResult<Ord.OrderResponse>> ReadListAsync(
        int page,
        int size,
        Guid? customer,
        OrderStatus? status,
        CancellationToken ct)
    {
        var rows = Current.Orders.Values.Where(
            x => (!customer.HasValue || x.CustomerId == customer) && (!status.HasValue || x.Status == status)).OrderByDescending(
            x => x.CreatedAt).ToArray();

        return Task.FromResult(
            new Ord.PagedResult<Ord.OrderResponse>(
                rows.Skip((page - 1) * size).Take(size).Select(Ord.OrderResponse.From).ToArray(),
                rows.Length,
                page,
                size));
    }

    protected override Task<IReadOnlyList<Guid>> ReadRecoveryIdsAsync(
        DateTime cutoff,
        int limit,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(
        Current.Orders.Keys.Take(limit).ToArray());

    protected override void Add(PurchaseOrder order) => Current.Orders.Add(order.Id, order);

    protected override Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(TransactionalState<OrderData>.Lease lease) : Ord.ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}

public sealed class CartData
{
    public Dictionary<Guid, ShoppingCart> Carts = [];

    public Dictionary<Guid, CheckoutAttempt> Checkouts = [];

}

public sealed class CartTestService(Car.ICatalogClient catalog, Car.IOrderClient orders, TransactionalState<CartData>? initialState = null) : ECommerce.Cart.Infrastructure.CartService(null!, catalog, orders)
{
    public TransactionalState<CartData> State { get; } = initialState ?? new();
    public CartData Data => State.Data;
    private CartData Current => State.Current;

    protected override Task<Car.ICartTransaction> BeginTransactionAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Car.ICartTransaction>(
        new Transaction(State.Start()));

    protected override Task<ShoppingCart?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Carts.GetValueOrDefault(id));

    protected override Task<CheckoutAttempt?> FindCheckoutAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Checkouts.GetValueOrDefault(id));

    protected override void Add(ShoppingCart cart) => Current.Carts.Add(cart.CustomerId, cart);

    protected override void Add(CheckoutAttempt checkout) => Current.Checkouts.Add(checkout.RequestId, checkout);

    protected override Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(TransactionalState<CartData>.Lease lease) : Car.ICartTransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}

public sealed class PaymentData
{
    public Dictionary<Guid, PaymentRecord> Payments = [];

}

public sealed class PaymentTestService(Pay.IOrderClient orders, Pay.IPaymentProvider provider, TransactionalState<PaymentData>? initialState = null) : ECommerce.Payment.Infrastructure.PaymentService(null!, orders, provider)
{
    public TransactionalState<PaymentData> State { get; } = initialState ?? new();
    public PaymentData Data => State.Data;
    private PaymentData Current => State.Current;

    protected override Task<Pay.IPaymentTransaction> BeginTransactionAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Pay.IPaymentTransaction>(
        new Transaction(State.Start()));

    protected override Task<PaymentRecord?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Payments.GetValueOrDefault(id));

    protected override Task<PaymentRecord?> FindActiveByOrderAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Payments.Values.SingleOrDefault(x => x.OrderId == id && x.Status != PaymentStatus.Failed));

    protected override Task<IReadOnlyList<Guid>> ReadRecoveryIdsAsync(
        int limit,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(
        Current.Payments.Keys.Take(limit).ToArray());

    protected override Task<Pay.PagedResult<Pay.PaymentResponse>> ReadListAsync(
        Guid? customer,
        int page,
        int size,
        CancellationToken ct)
    {
        var rows = Current.Payments.Values.Where(x => !customer.HasValue || x.CustomerId == customer).ToArray();

        return Task.FromResult(
            new Pay.PagedResult<Pay.PaymentResponse>(
                rows.Skip((page - 1) * size).Take(size).Select(Pay.PaymentResponse.From).ToArray(),
                rows.Length,
                page,
                size));
    }

    protected override void Add(PaymentRecord payment) => Current.Payments.Add(payment.Id, payment);

    protected override Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(TransactionalState<PaymentData>.Lease lease) : Pay.IPaymentTransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}
