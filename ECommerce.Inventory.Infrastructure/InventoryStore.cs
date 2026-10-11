using ECommerce.Inventory.Application;
using ECommerce.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace ECommerce.Inventory.Infrastructure;

public sealed class InventoryStore(InventoryDbContext db) : IInventoryStore
{
    public async Task<ITransaction> BeginAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new DomainException("Operation ID cannot be empty.");
        db.ChangeTracker.Clear();
        var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = "inventory:" + id;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            return new Transaction(tx);
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public async Task<IReadOnlyList<StockItem>> LockStocksAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var stocks = new List<StockItem>();
        foreach (var id in ids.Distinct().Order())
        {
            var rows = await db.Stocks.FromSqlInterpolated($"SELECT * FROM stock_items WHERE \"ProductId\" = {id} FOR UPDATE").ToListAsync(ct);
            stocks.AddRange(rows);
        }
        return stocks;
    }
    public Task<StockItem?> FindStockAsync(Guid id, CancellationToken ct) => db.Stocks.SingleOrDefaultAsync(x => x.ProductId == id, ct);
    public Task<Reservation?> FindReservationAsync(Guid id, CancellationToken ct) => db.Reservations.Include(x => x.Items).SingleOrDefaultAsync(x => x.OrderId == id, ct);
    public Task<StockAdjustment?> FindAdjustmentAsync(Guid id, CancellationToken ct) => db.Adjustments.SingleOrDefaultAsync(x => x.RequestId == id, ct);
    public async Task<PagedResult<StockResponse>> GetStocksAsync(int page, int size, int? maxAvailable, CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking();
        if (maxAvailable.HasValue) query = query.Where(x => x.OnHand - x.Reserved <= maxAvailable.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.ProductId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(StockResponse.From).ToArray(), count, page, size);
    }
    public async Task<PagedResult<AdjustmentResponse>> GetAdjustmentsAsync(Guid productId, int page, int size, CancellationToken ct)
    {
        var query = db.Adjustments.AsNoTracking().Where(x => x.ProductId == productId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.RequestId).Skip((page - 1) * size).Take(size)
            .Select(x => new AdjustmentResponse(x.RequestId, x.ProductId, x.Delta, x.Reason, x.CreatedAt)).ToListAsync(ct);
        return new(items, count, page, size);
    }
    public async Task<PagedResult<ReservationResponse>> GetReservationsAsync(int page, int size, ReservationStatus? status, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var count = await query.CountAsync(ct);
        var items = await query.Include(x => x.Items).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.OrderId)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select(ReservationResponse.From).ToArray(), count, page, size);
    }
    public void Add(StockItem stock) => db.Stocks.Add(stock);
    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
    public void Add(StockAdjustment adjustment) => db.Adjustments.Add(adjustment);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    private sealed class Transaction(IDbContextTransaction tx) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
