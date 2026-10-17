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

public sealed class InventoryStore : TransactionalState<InventoryData>, Inv.IInventoryStore
{
    public Task<Inv.ITransaction> BeginAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Inv.ITransaction>(
        new Transaction(Start()));

    public Task<IReadOnlyList<StockItem>> LockStocksAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<StockItem>>(
        ids.Distinct().Order().Where(Current.Stocks.ContainsKey).Select(x => Current.Stocks[x]).ToArray());

    public Task<StockItem?> FindStockAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Stocks.GetValueOrDefault(id));

    public Task<Reservation?> FindReservationAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Reservations.GetValueOrDefault(id));

    public Task<StockAdjustment?> FindAdjustmentAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Adjustments.GetValueOrDefault(id));

    public Task<Inv.PagedResult<Inv.StockResponse>> GetStocksAsync(int page, int size, int? max, CancellationToken ct)
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

    public Task<Inv.PagedResult<Inv.AdjustmentResponse>> GetAdjustmentsAsync(
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

    public Task<Inv.PagedResult<Inv.ReservationResponse>> GetReservationsAsync(
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

    public void Add(StockItem stock) => Current.Stocks.Add(stock.ProductId, stock);

    public void Add(Reservation reservation) => Current.Reservations.Add(reservation.OrderId, reservation);

    public void Add(StockAdjustment adjustment) => Current.Adjustments.Add(adjustment.RequestId, adjustment);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(Lease lease) : Inv.ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}

public sealed class OrderData
{
    public Dictionary<Guid, PurchaseOrder> Orders = [];

}

public sealed class OrderStore : TransactionalState<OrderData>, Ord.IOrderStore
{
    public Task<Ord.ITransaction> BeginAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Ord.ITransaction>(
        new Transaction(Start()));

    public Task<PurchaseOrder?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Orders.GetValueOrDefault(id));

    public Task<Ord.PagedResult<Ord.OrderResponse>> ListAsync(
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

    public Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(
        DateTime cutoff,
        int limit,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(
        Current.Orders.Keys.Take(limit).ToArray());

    public void Add(PurchaseOrder order) => Current.Orders.Add(order.Id, order);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(Lease lease) : Ord.ITransaction
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

public sealed class CartStore : TransactionalState<CartData>, Car.ICartStore
{
    public Task<Car.ICartTransaction> BeginAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Car.ICartTransaction>(
        new Transaction(Start()));

    public Task<ShoppingCart?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Carts.GetValueOrDefault(id));

    public Task<CheckoutAttempt?> FindCheckoutAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Checkouts.GetValueOrDefault(id));

    public void Add(ShoppingCart cart) => Current.Carts.Add(cart.CustomerId, cart);

    public void Add(CheckoutAttempt checkout) => Current.Checkouts.Add(checkout.RequestId, checkout);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(Lease lease) : Car.ICartTransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}

public sealed class PaymentData
{
    public Dictionary<Guid, PaymentRecord> Payments = [];

}

public sealed class PaymentStore : TransactionalState<PaymentData>, Pay.IPaymentStore
{
    public Task<Pay.IPaymentTransaction> BeginAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult<Pay.IPaymentTransaction>(
        new Transaction(Start()));

    public Task<PaymentRecord?> FindAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Payments.GetValueOrDefault(id));

    public Task<PaymentRecord?> FindActiveByOrderAsync(
        Guid id,
        CancellationToken ct) => Task.FromResult(
        Current.Payments.Values.SingleOrDefault(x => x.OrderId == id && x.Status != PaymentStatus.Failed));

    public Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(
        int limit,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(
        Current.Payments.Keys.Take(limit).ToArray());

    public Task<Pay.PagedResult<Pay.PaymentResponse>> ListAsync(
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

    public void Add(PaymentRecord payment) => Current.Payments.Add(payment.Id, payment);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class Transaction(Lease lease) : Pay.IPaymentTransaction
    {
        public Task CommitAsync(CancellationToken ct) => lease.Commit(ct);

        public ValueTask DisposeAsync() => lease.Dispose();

    }

}
