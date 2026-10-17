using ECommerce.Order.Application;
using ECommerce.Order.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Order.Infrastructure;

public sealed class OrderStore(OrderDbContext db) : IOrderStore
{
    public async Task<ITransaction> BeginAsync(Guid id, CancellationToken ct)
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

    public Task<PurchaseOrder?> FindAsync(
        Guid id,
        CancellationToken ct) => db.Orders.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.Id == id,
        ct);

    public async Task<PagedResult<OrderResponse>> ListAsync(
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

    public void Add(PurchaseOrder order) => db.Orders.Add(order);

    public async Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(
        DateTime cutoff,
        int limit,
        CancellationToken ct) => await db.Orders.AsNoTracking().Where(
        x => x.Status == OrderStatus.PendingInventory || x.Status == OrderStatus.Confirming || x.Status == OrderStatus.Cancelling || (x.Status == OrderStatus.Reserved && x.CreatedAt <= cutoff)).OrderBy(
        x => x.LastRecoveryAttemptAt ?? x.CreatedAt).ThenBy(
        x => x.Id).Take(limit).Select(
        x => x.Id).ToListAsync(ct);

    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

}
