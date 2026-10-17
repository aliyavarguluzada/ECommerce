using ECommerce.Order.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Order.Domain;

namespace ECommerce.Order.Infrastructure;

public class OrderService(
    OrderDbContext db,
    ICatalogClient catalog,
    IInventoryClient inventory,
    IPaymentClient payments) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.CustomerId == Guid.Empty || request.ShippingAddress is null || request.Items is null || request.Items.Count is < 1 or > 100 || request.Items.Any(
            x => x is null || x.ProductId == Guid.Empty || x.Quantity is < 1 or > 100000) || request.Items.Select(
            x => x.ProductId).Distinct().Count() != request.Items.Count)
        {
            throw new DomainException("Request ID, customer, valid address, and 1–100 distinct products with quantities 1–100000 are required.");

        }

        var address = request.ShippingAddress.ToDomain();

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new {
                            request.CustomerId,
                            Address = address,
                            Items = request.Items.OrderBy(x => x.ProductId).ToArray() }))));

        await using (var tx = await BeginTransactionAsync(request.RequestId, ct))
        {
            var existing = await FindAsync(request.RequestId, ct);

            if (existing is not null)
            {
                if (existing.RequestFingerprint != fingerprint)
                {
                    throw new ServiceException(409, "Request ID was already used for a different order.");

                }

                await tx.CommitAsync(ct);

            }

            else
            {
                var lines = new List<OrderLine>();

                string? currency = null;

                foreach (var item in request.Items.OrderBy(x => x.ProductId))
                {
                    var product = await catalog.GetAsync(item.ProductId, ct);

                    if (!product.IsActive)
                    {
                        throw new ServiceException(400, $"Product {item.ProductId} is discontinued.");

                    }

                    currency ??= product.Currency;

                    if (currency != product.Currency)
                    {
                        throw new DomainException("All order items must have the same currency.");

                    }

                    lines.Add(new OrderLine(item.ProductId, product.Name, item.Quantity, product.Price));

                }

                Add(
                    PurchaseOrder.Create(
                        request.RequestId,
                        request.CustomerId,
                        fingerprint,
                        currency!,
                        address,
                        lines));

                await SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

            }

        }

        try
        {
            return await RetryAsync(request.RequestId, ct);
        }

        catch (ServiceException error)when (error.StatusCode is 503 or 409)
        {
            return await GetAsync(request.RequestId, ct);
        }

    }

    public async Task<OrderResponse> GetAsync(
        Guid id,
        CancellationToken ct) => OrderResponse.From(
        await Required(id, ct));

    public Task<PagedResult<OrderResponse>> ListAsync(
        int page,
        int size,
        Guid? customerId,
        OrderStatus? status,
        CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue || customerId == Guid.Empty || (status.HasValue && !Enum.IsDefined(
            status.Value)))
        {
            throw new DomainException("Invalid pagination or order filters.");

        }

        return ReadListAsync(page, size, customerId, status, ct);
    }

    public async Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken ct)
    {
        await using (var tx = await BeginTransactionAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered)
            {
                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
            }

            if (order.Status != OrderStatus.Confirming)
            {
                order.BeginConfirmation();

                await SaveChangesAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken ct)
    {
        await using (var tx = await BeginTransactionAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.Status == OrderStatus.Cancelled)
            {
                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
            }

            if (order.Status != OrderStatus.Cancelling)
            {
                order.BeginCancellation(request.Reason);

                await SaveChangesAsync(ct);

            }

            else if (order.CancellationReason != request.Reason?.Trim())
            {
                throw new ServiceException(409, "Cancellation is already in progress with a different reason.");

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        await using (var tx = await BeginTransactionAsync(id, ct))
        {
            var order = await Required(id, ct);

            order.RecordPayment(request.PaymentId, request.Amount, request.Currency);

            if (order.Status == OrderStatus.Reserved)
            {
                order.BeginConfirmation();

            }

            else if (order.Status is not (OrderStatus.Confirming or OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered))
            {
                throw new ServiceException(409, "Order inventory is not ready for payment confirmation.");

            }

            await SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> RetryAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(id, ct);

        var order = await Required(id, ct);

        switch (order.Status)
        {
            case OrderStatus.PendingInventory:

                var reserve = await inventory.ReserveAsync(
                    id,
                    order.Items.Select(x => new OrderItemRequest(x.ProductId, x.Quantity)),
                    ct);

                if (reserve.Status != "Reserved")
                {
                    throw new ServiceException(409, "Inventory reservation is not in the expected state.");

                }

                order.MarkReserved();

                break;

            case OrderStatus.Confirming:

                var commit = await inventory.CommitAsync(id, ct);

                if (commit.Status != "Committed")
                {
                    throw new ServiceException(409, "Inventory commit is not in the expected state.");

                }

                order.Confirm();

                break;

            case OrderStatus.Cancelling:

                var release = await inventory.ReleaseAsync(id, ct);

                if (release.Status != "Released")
                {
                    throw new ServiceException(409, "Inventory release is not in the expected state.");

                }

                if (order.PaymentId.HasValue && !order.PaymentRefunded)
                {
                    var refund = await payments.RefundAsync(id, ct);

                    if (refund.Status != "Refunded" || refund.PaymentId != order.PaymentId)
                    {
                        throw new ServiceException(503, "Payment refund remains pending.");

                    }

                    order.RecordRefund();

                }

                order.Cancel();

                break;

            default:

                await tx.CommitAsync(ct);

                return OrderResponse.From(order);
        }

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> ShipAsync(Guid id, ShipOrderRequest request, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(id, ct);

        var order = await Required(id, ct);

        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            if (order.Carrier != request.Carrier?.Trim() || order.TrackingNumber != request.TrackingNumber?.Trim())
            {
                throw new ServiceException(409, "Order was already shipped with different tracking details.");

            }

        }

        else
        {
            order.Ship(request.Carrier, request.TrackingNumber);

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> RecoverAsync(Guid id, DateTime expiryCutoff, CancellationToken ct)
    {
        await using (var tx = await BeginTransactionAsync(id, ct))
        {
            var order = await Required(id, ct);

            if (order.CreatedAt <= expiryCutoff && order.Status is (OrderStatus.PendingInventory or OrderStatus.Reserved))
            {
                order.BeginCancellation("Unconfirmed order reservation expired.");

            }

            if (order.Status is OrderStatus.PendingInventory or OrderStatus.Confirming or OrderStatus.Cancelling)
            {
                order.RecordRecoveryAttempt();

                await SaveChangesAsync(ct);

            }

            await tx.CommitAsync(ct);

        }

        return await RetryAsync(id, ct);
    }

    public async Task<OrderResponse> DeliverAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(id, ct);

        var order = await Required(id, ct);

        if (order.Status != OrderStatus.Delivered)
        {
            order.Deliver();

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

        return OrderResponse.From(order);
    }

    private async Task<PurchaseOrder> Required(
        Guid id,
        CancellationToken ct) => await FindAsync(
        id,
        ct) ?? throw new ServiceException(
        404,
        "Order not found.");

    protected virtual async Task<ITransaction> BeginTransactionAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Order ID cannot be empty.");

        }

        db.ChangeTracker.Clear();

        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var key = "order:" + id;

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                ct);

            return new Transaction(tx);
        }

        catch
        {
            await tx.DisposeAsync();

            throw;

        }

    }

    protected virtual Task<PurchaseOrder?> FindAsync(
        Guid id,
        CancellationToken ct) => db.Orders.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.Id == id,
        ct);

    protected virtual async Task<PagedResult<OrderResponse>> ReadListAsync(
        int page,
        int size,
        Guid? customerId,
        OrderStatus? status,
        CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);

        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);

        }

        var count = await query.CountAsync(ct);

        var rows = await query.Include(
            x => x.Items).OrderByDescending(
            x => x.CreatedAt).ThenBy(
            x => x.Id).Skip(
            (page - 1) * size).Take(size).ToListAsync(ct);

        return new(rows.Select(OrderResponse.From).ToArray(), count, page, size);
    }

    protected virtual void Add(PurchaseOrder order) => db.Orders.Add(order);

    protected virtual async Task<IReadOnlyList<Guid>> ReadRecoveryIdsAsync(
        DateTime cutoff,
        int limit,
        CancellationToken ct) => await db.Orders.AsNoTracking().Where(
        x => x.Status == OrderStatus.PendingInventory || x.Status == OrderStatus.Confirming || x.Status == OrderStatus.Cancelling || (x.Status == OrderStatus.Reserved && x.CreatedAt <= cutoff)).OrderBy(
        x => x.LastRecoveryAttemptAt ?? x.CreatedAt).ThenBy(
        x => x.Id).Take(limit).Select(
        x => x.Id).ToListAsync(ct);

    protected virtual async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

    public Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(DateTime cutoff, int limit, CancellationToken ct) => ReadRecoveryIdsAsync(cutoff, limit, ct);
}
