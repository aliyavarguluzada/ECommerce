using ECommerce.Payment.Application;
using ECommerce.Payment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace ECommerce.Payment.Infrastructure;
public sealed class PaymentStore(PaymentDbContext db) : IPaymentStore
{
    public async Task<IPaymentTransaction> BeginAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var tx = await db.Database.BeginTransactionAsync(ct);
        try { var key = "payment:" + id; await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct); return new Transaction(tx); }
        catch { await tx.DisposeAsync(); throw; }
    }
    public Task<PaymentRecord?> FindAsync(Guid id, CancellationToken ct) => db.Payments.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PaymentRecord?> FindActiveByOrderAsync(Guid id, CancellationToken ct) => db.Payments.SingleOrDefaultAsync(x => x.OrderId == id && x.Status != PaymentStatus.Failed, ct);
    public async Task<IReadOnlyList<Guid>> GetRecoveryIdsAsync(int limit, CancellationToken ct) =>
        await db.Payments.AsNoTracking().Where(x => x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Refunding ||
            (x.Status == PaymentStatus.Succeeded && !x.OrderNotified)).OrderBy(x => x.LastRecoveryAttemptAt ?? x.CreatedAt).Take(limit).Select(x => x.Id).ToListAsync(ct);
    public async Task<PagedResult<PaymentResponse>> ListAsync(Guid? customer, int page, int size, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking(); if (customer.HasValue) query = query.Where(x => x.CustomerId == customer.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(PaymentResponse.From).ToArray(), count, page, size);
    }
    public void Add(PaymentRecord payment) => db.Payments.Add(payment);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    private sealed class Transaction(IDbContextTransaction tx) : IPaymentTransaction
    { public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct); public ValueTask DisposeAsync() => tx.DisposeAsync(); }
}
