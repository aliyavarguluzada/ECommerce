using System.Text.Json.Serialization;
using ECommerce.Inventory.Domain;

namespace ECommerce.Inventory.Application;

public sealed record CreateStockRequest([property: JsonRequired] Guid ProductId, [property: JsonRequired] int Quantity);

public sealed record AdjustStockRequest(Guid RequestId, [property: JsonRequired] int Delta, string Reason);

public sealed record ReserveStockRequest(Guid OrderId, IReadOnlyList<ReserveLineRequest> Items);

public sealed record ReserveLineRequest(Guid ProductId, int Quantity);

public sealed record StockResponse(
    Guid ProductId,
    int OnHand,
    int Reserved,
    int Available,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static StockResponse From(
        StockItem stock) => new(
        stock.ProductId,
        stock.OnHand,
        stock.Reserved,
        stock.Available,
        stock.CreatedAt,
        stock.UpdatedAt);

}

public sealed record ReservationResponse(
    Guid OrderId,
    ReservationStatus Status,
    IReadOnlyList<ReserveLineRequest> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ReservationResponse From(
        Reservation reservation) => new(
        reservation.OrderId,
        reservation.Status,
        reservation.Items.Select(x => new ReserveLineRequest(x.ProductId, x.Quantity)).ToArray(),
        reservation.CreatedAt,
        reservation.UpdatedAt);

}

public sealed record AdjustmentResponse(Guid RequestId, Guid ProductId, int Delta, string Reason, DateTime CreatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record CatalogProduct(
    Guid Id,
    string Name,
    [property: JsonRequired] decimal Price,
    string Currency,
    [property: JsonRequired] bool IsActive);

public static class Paging
{
    public static void Validate(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
        {
            throw new DomainException("Page must be positive, size must be 1–100, and page offset must fit a 32-bit integer.");
        }

    }

}
