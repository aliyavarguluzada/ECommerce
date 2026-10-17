using ECommerce.Order.Domain;
using System.Text.Json.Serialization;

namespace ECommerce.Order.Application;

public sealed record CreateOrderRequest(
    Guid RequestId,
    Guid CustomerId,
    AddressRequest ShippingAddress,
    IReadOnlyList<OrderItemRequest> Items);

public sealed record AddressRequest(
    string Recipient,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode)
{
    public ShippingAddress ToDomain() => new(Recipient, Line1, Line2, City, Region, PostalCode, CountryCode);

}

public sealed record OrderItemRequest(Guid ProductId, int Quantity);

public sealed record CancelOrderRequest(string Reason);

public sealed record ShipOrderRequest(string Carrier, string TrackingNumber);

public sealed record OrderLineResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    string Currency,
    decimal Total,
    ShippingAddress ShippingAddress,
    IReadOnlyList<OrderLineResponse> Items,
    string? CancellationReason,
    string? Carrier,
    string? TrackingNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? PaymentId,
    bool PaymentRefunded)
{
    public static OrderResponse From(
        PurchaseOrder order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.Currency,
        order.Total,
        order.ShippingAddress,
        order.Items.Select(
            x => new OrderLineResponse(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.LineTotal)).ToArray(),
        order.CancellationReason,
        order.Carrier,
        order.TrackingNumber,
        order.CreatedAt,
        order.UpdatedAt,
        order.PaymentId,
        order.PaymentRefunded);

}

public sealed record RecordPaymentRequest(Guid PaymentId, decimal Amount, string Currency);

public sealed record RefundSnapshot(Guid PaymentId, string Status);

public sealed record OrderSummary(Guid Id, Guid CustomerId, OrderStatus Status, string Currency, decimal Total);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record CatalogProduct(
    Guid Id,
    string Name,
    [property: JsonRequired] decimal Price,
    string Currency,
    [property: JsonRequired] bool IsActive);

public sealed record ReservationSnapshot(Guid OrderId, string Status);
