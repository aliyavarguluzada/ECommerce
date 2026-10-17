using ECommerce.Cart.Domain;

namespace ECommerce.Cart.Application;

public interface ICartService
{
    Task<CartResponse> GetAsync(Guid customer, CancellationToken ct);

    Task SetAsync(Guid customer, Guid productId, int quantity, CancellationToken ct);

    Task RemoveAsync(Guid customer, Guid productId, CancellationToken ct);

    Task ClearAsync(Guid customer, CancellationToken ct);

    Task<OrderSnapshot> CheckoutAsync(Guid customer, CheckoutRequest request, CancellationToken ct);

    Task<OrderSnapshot> RetryCheckoutAsync(Guid customer, CancellationToken ct);

}
