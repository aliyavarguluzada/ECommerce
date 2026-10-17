using ECommerce.Inventory.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ECommerce.Inventory.Domain;

namespace ECommerce.Inventory.Infrastructure;

public class InventoryService(InventoryDbContext db, ICatalogClient catalog) : IInventoryService
{
    public async Task<StockResponse> CreateAsync(CreateStockRequest request, CancellationToken ct)
    {
        var stock = StockItem.Create(request.ProductId, request.Quantity);

        var product = await catalog.GetAsync(request.ProductId, ct);

        if (!product.IsActive)
        {
            throw new ServiceException(400, "Discontinued products cannot be added to inventory.");
        }

        await using var tx = await BeginTransactionAsync(request.ProductId, ct);

        if (await FindStockAsync(request.ProductId, ct)is not null)
        {
            throw new ServiceException(409, "Stock already exists for this product.");
        }

        Add(stock);

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return StockResponse.From(stock);
    }

    public async Task<StockResponse> GetAsync(
        Guid id,
        CancellationToken ct) => StockResponse.From(
        await FindStockAsync(id, ct) ?? throw new ServiceException(404, "Stock item not found."));

    public Task<PagedResult<StockResponse>> ListAsync(int page, int size, int? maxAvailable, CancellationToken ct)
    {
        Paging.Validate(page, size);

        if (maxAvailable < 0)
        {
            throw new DomainException("Low-stock threshold cannot be negative.");
        }

        return ReadStocksAsync(page, size, maxAvailable, ct);
    }

    public Task<PagedResult<AdjustmentResponse>> AdjustmentsAsync(
        Guid productId,
        int page,
        int size,
        CancellationToken ct)
    {
        Paging.Validate(page, size);

        return ReadAdjustmentsAsync(productId, page, size, ct);
    }

    public async Task<StockResponse> AdjustAsync(Guid productId, AdjustStockRequest request, CancellationToken ct)
    {
        var adjustment = StockAdjustment.Create(request.RequestId, productId, request.Delta, request.Reason);

        await using var tx = await BeginTransactionAsync(request.RequestId, ct);

        var existing = await FindAdjustmentAsync(request.RequestId, ct);

        if (existing is not null && !existing.Matches(adjustment))
        {
            throw new ServiceException(409, "Request ID was already used for another adjustment.");
        }

        var stock = (await LockStocksAsync(
            [productId],
            ct)).SingleOrDefault() ?? throw new ServiceException(
            404,
            "Stock item not found.");

        if (existing is null)
        {
            stock.Adjust(request.Delta);

            Add(adjustment);

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

        return StockResponse.From(stock);
    }

    public async Task<ReservationResponse> GetReservationAsync(
        Guid id,
        CancellationToken ct) => ReservationResponse.From(
        await FindReservationAsync(id, ct) ?? throw new ServiceException(404, "Reservation not found."));

    public Task<PagedResult<ReservationResponse>> ReservationsAsync(
        int page,
        int size,
        ReservationStatus? status,
        CancellationToken ct)
    {
        Paging.Validate(page, size);

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new DomainException("Invalid reservation status.");
        }

        return ReadReservationsAsync(page, size, status, ct);
    }

    public async Task<ReservationResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Any(x => x is null))
        {
            throw new DomainException("Reservation items are required.");
        }

        var reservation = Reservation.Create(
            request.OrderId,
            request.Items.Select(x => new ReservationLine(x.ProductId, x.Quantity)));

        await using var tx = await BeginTransactionAsync(request.OrderId, ct);

        var existing = await FindReservationAsync(request.OrderId, ct);

        if (existing is not null)
        {
            if (existing.Status == ReservationStatus.Released || !existing.Matches(reservation.Items))
            {
                throw new ServiceException(
                    409,
                    "Order reservation is released or its items differ from the original request.");
            }

            await tx.CommitAsync(ct);

            return ReservationResponse.From(existing);
        }

        var stocks = (await LockStocksAsync(
            reservation.Items.Select(x => x.ProductId),
            ct)).ToDictionary(
            x => x.ProductId);

        foreach (var line in reservation.Items)
        {
            if (!stocks.TryGetValue(line.ProductId, out var stock))
            {
                throw new ServiceException(409, $"Product {line.ProductId} has no inventory.");
            }

            if (stock.Available < line.Quantity)
            {
                throw new ServiceException(409, $"Insufficient stock for product {line.ProductId}.");
            }

            stock.Reserve(line.Quantity);

        }

        Add(reservation);

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return ReservationResponse.From(reservation);
    }

    public async Task<ReservationResponse> CommitAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(id, ct);

        var reservation = await FindReservationAsync(
            id,
            ct) ?? throw new ServiceException(
            404,
            "Reservation not found.");

        if (reservation.Status == ReservationStatus.Released)
        {
            throw new ServiceException(409, "Released reservation cannot be committed.");
        }

        if (reservation.Status == ReservationStatus.Reserved)
        {
            var stocks = (await LockStocksAsync(
                reservation.Items.Select(x => x.ProductId),
                ct)).ToDictionary(
                x => x.ProductId);

            foreach (var line in reservation.Items)
            {
                stocks[line.ProductId].Commit(line.Quantity);

            }

            reservation.Commit();

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

        return ReservationResponse.From(reservation);
    }

    public async Task<ReservationResponse> ReleaseAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(id, ct);

        var reservation = await FindReservationAsync(id, ct);

        if (reservation is null)
        {
            reservation = Reservation.ReleasedTombstone(id);

            Add(reservation);

            await SaveChangesAsync(ct);

        }

        else if (reservation.Status != ReservationStatus.Released)
        {
            var stocks = (await LockStocksAsync(
                reservation.Items.Select(x => x.ProductId),
                ct)).ToDictionary(
                x => x.ProductId);

            foreach (var line in reservation.Items)
            {
                if (reservation.Status == ReservationStatus.Committed)
                {
                    stocks[line.ProductId].Restore(line.Quantity);

                }

                else
                {
                    stocks[line.ProductId].Release(line.Quantity);

                }

            }

            reservation.Release();

            await SaveChangesAsync(ct);

        }

        await tx.CommitAsync(ct);

        return ReservationResponse.From(reservation);
    }

    protected virtual async Task<ITransaction> BeginTransactionAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Operation ID cannot be empty.");
        }

        db.ChangeTracker.Clear();

        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var key = "inventory:" + id;

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

    protected virtual async Task<IReadOnlyList<StockItem>> LockStocksAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var stocks = new List<StockItem>();

        foreach (var id in ids.Distinct().Order())
        {
            var rows = await db.Stocks.FromSqlInterpolated(
                $"SELECT * FROM stock_items WHERE \"ProductId\" = {id} FOR UPDATE").ToListAsync(ct);

            stocks.AddRange(rows);

        }

        return stocks;
    }

    protected virtual Task<StockItem?> FindStockAsync(
        Guid id,
        CancellationToken ct) => db.Stocks.SingleOrDefaultAsync(
        x => x.ProductId == id,
        ct);

    protected virtual Task<Reservation?> FindReservationAsync(
        Guid id,
        CancellationToken ct) => db.Reservations.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.OrderId == id,
        ct);

    protected virtual Task<StockAdjustment?> FindAdjustmentAsync(
        Guid id,
        CancellationToken ct) => db.Adjustments.SingleOrDefaultAsync(
        x => x.RequestId == id,
        ct);

    protected virtual async Task<PagedResult<StockResponse>> ReadStocksAsync(
        int page,
        int size,
        int? maxAvailable,
        CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking();

        if (maxAvailable.HasValue)
        {
            query = query.Where(x => x.OnHand - x.Reserved <= maxAvailable.Value);

        }

        var count = await query.CountAsync(ct);

        var items = await query.OrderBy(x => x.ProductId).Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return new(items.Select(StockResponse.From).ToArray(), count, page, size);
    }

    protected virtual async Task<PagedResult<AdjustmentResponse>> ReadAdjustmentsAsync(
        Guid productId,
        int page,
        int size,
        CancellationToken ct)
    {
        var query = db.Adjustments.AsNoTracking().Where(x => x.ProductId == productId);

        var count = await query.CountAsync(ct);

        var items = await query.OrderByDescending(
            x => x.CreatedAt).ThenBy(
            x => x.RequestId).Skip(
            (page - 1) * size).Take(size).Select(
            x => new AdjustmentResponse(x.RequestId, x.ProductId, x.Delta, x.Reason, x.CreatedAt)).ToListAsync(ct);

        return new(items, count, page, size);
    }

    protected virtual async Task<PagedResult<ReservationResponse>> ReadReservationsAsync(
        int page,
        int size,
        ReservationStatus? status,
        CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);

        }

        var count = await query.CountAsync(ct);

        var items = await query.Include(
            x => x.Items).OrderByDescending(
            x => x.CreatedAt).ThenBy(
            x => x.OrderId).Skip(
            (page - 1) * size).Take(size).ToListAsync(ct);

        return new(items.Select(ReservationResponse.From).ToArray(), count, page, size);
    }

    protected virtual void Add(StockItem stock) => db.Stocks.Add(stock);

    protected virtual void Add(Reservation reservation) => db.Reservations.Add(reservation);

    protected virtual void Add(StockAdjustment adjustment) => db.Adjustments.Add(adjustment);

    protected virtual async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

}
