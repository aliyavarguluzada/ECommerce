namespace ECommerce.Cart.Application;

public sealed record SetQuantityCommand(int Quantity);

public sealed record AddressRequest(
    string Recipient,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode)
{
    public AddressRequest Normalize()
    {
        static string Required(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > length)
            {
                throw new ServiceException(400, "Invalid shipping address.");

            }

            return value.Trim();
        }

        static string? Optional(string? value, int length)
        {
            if (value?.Trim().Length > length)
            {
                throw new ServiceException(400, "Invalid shipping address.");

            }

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        var country = Required(CountryCode, 2).ToUpperInvariant();

        if (country.Length != 2 || country.Any(x => x is < 'A' or > 'Z'))
        {
            throw new ServiceException(400, "Country code must contain two letters.");

        }

        return new(
            Required(Recipient, 150),
            Required(Line1, 200),
            Optional(Line2, 200),
            Required(City, 100),
            Optional(Region, 100),
            Required(PostalCode, 20),
            country);
    }

}

public sealed record CheckoutCommand(Guid RequestId, AddressRequest ShippingAddress);

public sealed record CatalogProduct(Guid Id, string Name, decimal Price, string Currency, bool IsActive);

public sealed record CartItemResponse(
    Guid ProductId,
    int Quantity,
    string? ProductName,
    decimal? UnitPrice,
    string? Currency,
    bool IsAvailable);

public sealed record CartResponse(
    Guid CustomerId,
    Guid? PendingCheckoutId,
    IReadOnlyList<CartItemResponse> Items,
    bool CanCheckout,
    decimal? Subtotal,
    string? Currency,
    DateTime UpdatedAt);

public sealed record OrderSnapshot(Guid Id, Guid CustomerId, string Status, string Currency, decimal Total);

public interface ICartTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);

}

public interface ICartStore
{
    Task<ICartTransaction> BeginAsync(Guid customerId, CancellationToken ct);

    Task<Domain.ShoppingCart?> FindAsync(Guid customerId, CancellationToken ct);

    Task<Domain.CheckoutAttempt?> FindCheckoutAsync(Guid requestId, CancellationToken ct);

    void Add(Domain.ShoppingCart cart);

    void Add(Domain.CheckoutAttempt attempt);

    Task SaveAsync(CancellationToken ct);

}

public interface ICatalogClient
{
    Task<CatalogProduct?> GetAsync(Guid id, CancellationToken ct);

}

public interface IOrderClient
{
    Task<OrderSnapshot> CreateAsync(
        Guid id,
        Guid customer,
        AddressRequest address,
        IEnumerable<Domain.CartLine> items,
        CancellationToken ct);

    Task<OrderSnapshot> GetAsync(Guid id, CancellationToken ct);

}
