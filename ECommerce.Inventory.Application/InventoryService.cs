using ECommerce.Inventory.Domain;
namespace ECommerce.Inventory.Application;

public sealed class InventoryService(IInventoryStore store, ICatalogClient catalog)
{
    public async Task<StockResponse> CreateAsync(CreateStockCommand command, CancellationToken ct)
    {
        var stock = StockItem.Create(command.ProductId, command.Quantity);
        var product = await catalog.GetAsync(command.ProductId, ct);
        if (!product.IsActive) throw new ServiceException(400, "Discontinued products cannot be added to inventory.");
        await using var tx = await store.BeginAsync(command.ProductId, ct);
        if (await store.FindStockAsync(command.ProductId, ct) is not null) throw new ServiceException(409, "Stock already exists for this product.");
        store.Add(stock); await store.SaveAsync(ct); await tx.CommitAsync(ct); return StockResponse.From(stock);
    }
    public async Task<StockResponse> GetAsync(Guid id, CancellationToken ct) =>
        StockResponse.From(await store.FindStockAsync(id, ct) ?? throw new ServiceException(404, "Stock item not found."));
    public Task<PagedResult<StockResponse>> ListAsync(int page, int size, int? maxAvailable, CancellationToken ct)
    {
        Paging.Validate(page, size);
        if (maxAvailable < 0) throw new DomainException("Low-stock threshold cannot be negative.");
        return store.GetStocksAsync(page, size, maxAvailable, ct);
    }
    public Task<PagedResult<AdjustmentResponse>> AdjustmentsAsync(Guid productId, int page, int size, CancellationToken ct)
    {
        Paging.Validate(page, size); return store.GetAdjustmentsAsync(productId, page, size, ct);
    }
    public async Task<StockResponse> AdjustAsync(Guid productId, AdjustStockCommand command, CancellationToken ct)
    {
        var adjustment = StockAdjustment.Create(command.RequestId, productId, command.Delta, command.Reason);
        await using var tx = await store.BeginAsync(command.RequestId, ct);
        var existing = await store.FindAdjustmentAsync(command.RequestId, ct);
        if (existing is not null && !existing.Matches(adjustment)) throw new ServiceException(409, "Request ID was already used for another adjustment.");
        var stock = (await store.LockStocksAsync([productId], ct)).SingleOrDefault()
            ?? throw new ServiceException(404, "Stock item not found.");
        if (existing is null)
        {
            stock.Adjust(command.Delta); store.Add(adjustment); await store.SaveAsync(ct);
        }
        await tx.CommitAsync(ct); return StockResponse.From(stock);
    }
    public async Task<ReservationResponse> GetReservationAsync(Guid id, CancellationToken ct) =>
        ReservationResponse.From(await store.FindReservationAsync(id, ct) ?? throw new ServiceException(404, "Reservation not found."));
    public Task<PagedResult<ReservationResponse>> ReservationsAsync(int page, int size, ReservationStatus? status, CancellationToken ct)
    {
        Paging.Validate(page, size);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new DomainException("Invalid reservation status.");
        return store.GetReservationsAsync(page, size, status, ct);
    }
    public async Task<ReservationResponse> ReserveAsync(ReserveStockCommand command, CancellationToken ct)
    {
        if (command.Items is null || command.Items.Any(x => x is null)) throw new DomainException("Reservation items are required.");
        var reservation = Reservation.Create(command.OrderId, command.Items.Select(x => new ReservationLine(x.ProductId, x.Quantity)));
        await using var tx = await store.BeginAsync(command.OrderId, ct);
        var existing = await store.FindReservationAsync(command.OrderId, ct);
        if (existing is not null)
        {
            if (existing.Status == ReservationStatus.Released || !existing.Matches(reservation.Items))
                throw new ServiceException(409, "Order reservation is released or its items differ from the original request.");
            await tx.CommitAsync(ct); return ReservationResponse.From(existing);
        }
        var stocks = (await store.LockStocksAsync(reservation.Items.Select(x => x.ProductId), ct)).ToDictionary(x => x.ProductId);
        foreach (var line in reservation.Items)
        {
            if (!stocks.TryGetValue(line.ProductId, out var stock)) throw new ServiceException(409, $"Product {line.ProductId} has no inventory.");
            if (stock.Available < line.Quantity) throw new ServiceException(409, $"Insufficient stock for product {line.ProductId}.");
            stock.Reserve(line.Quantity);
        }
        store.Add(reservation); await store.SaveAsync(ct); await tx.CommitAsync(ct); return ReservationResponse.From(reservation);
    }
    public async Task<ReservationResponse> CommitAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(id, ct);
        var reservation = await store.FindReservationAsync(id, ct) ?? throw new ServiceException(404, "Reservation not found.");
        if (reservation.Status == ReservationStatus.Released) throw new ServiceException(409, "Released reservation cannot be committed.");
        if (reservation.Status == ReservationStatus.Reserved)
        {
            var stocks = (await store.LockStocksAsync(reservation.Items.Select(x => x.ProductId), ct)).ToDictionary(x => x.ProductId);
            foreach (var line in reservation.Items) stocks[line.ProductId].Commit(line.Quantity);
            reservation.Commit(); await store.SaveAsync(ct);
        }
        await tx.CommitAsync(ct); return ReservationResponse.From(reservation);
    }
    public async Task<ReservationResponse> ReleaseAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginAsync(id, ct);
        var reservation = await store.FindReservationAsync(id, ct);
        if (reservation is null) { reservation = Reservation.ReleasedTombstone(id); store.Add(reservation); await store.SaveAsync(ct); }
        else if (reservation.Status != ReservationStatus.Released)
        {
            var stocks = (await store.LockStocksAsync(reservation.Items.Select(x => x.ProductId), ct)).ToDictionary(x => x.ProductId);
            foreach (var line in reservation.Items)
            {
                if (reservation.Status == ReservationStatus.Committed) stocks[line.ProductId].Restore(line.Quantity);
                else stocks[line.ProductId].Release(line.Quantity);
            }
            reservation.Release(); await store.SaveAsync(ct);
        }
        await tx.CommitAsync(ct); return ReservationResponse.From(reservation);
    }
}
