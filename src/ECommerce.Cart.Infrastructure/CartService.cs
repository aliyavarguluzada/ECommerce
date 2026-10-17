using ECommerce.Cart.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Cart.Domain;

namespace ECommerce.Cart.Infrastructure;

public class CartService(CartDbContext db, ICatalogClient catalog, IOrderClient orders) : ICartService
{
    public async Task<CartResponse> GetAsync(Guid customer, CancellationToken ct)
    {
        var cart = await FindAsync(customer, ct) ?? ShoppingCart.Create(customer);

        var items = new List<CartItemResponse>();

        foreach (var line in cart.Items)
        {
            var product = await catalog.GetAsync(line.ProductId, ct);

            items.Add(
                new(
                    line.ProductId,
                    line.Quantity,
                    product?.Name,
                    product?.Price,
                    product?.Currency,
                    product?.IsActive == true));

        }

        var valid = items.Count > 0 && items.All(
            x => x.IsAvailable) && items.Select(
            x => x.Currency).Distinct().Count() == 1;

        var subtotal = valid ? items.Sum(x => x.UnitPrice!.Value * x.Quantity) : (decimal? )null;

        if (subtotal > 9999999999999999.99m)
        {
            valid = false;

        }

        return new(
            customer,
            cart.PendingCheckoutId,
            items,
            valid && !cart.PendingCheckoutId.HasValue,
            valid ? subtotal : null,
            valid ? items[0].Currency : null,
            cart.UpdatedAt);
    }

    public async Task SetAsync(Guid customer, Guid productId, int quantity, CancellationToken ct)
    {
        var product = await catalog.GetAsync(productId, ct);

        if (product is null || !product.IsActive)
        {
            throw new ServiceException(400, "Product does not exist or is discontinued.");

        }

        await using var tx = await BeginTransactionAsync(customer, ct);

        var cart = await GetOrCreate(customer, ct);

        cart.SetQuantity(productId, quantity);

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

    }

    public async Task RemoveAsync(Guid customer, Guid productId, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(customer, ct);

        var cart = await GetOrCreate(customer, ct);

        cart.Remove(productId);

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

    }

    public async Task ClearAsync(Guid customer, CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(customer, ct);

        var cart = await GetOrCreate(customer, ct);

        cart.Clear();

        await SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

    }

    public async Task<OrderSnapshot> CheckoutAsync(Guid customer, CheckoutRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.ShippingAddress is null)
        {
            throw new ServiceException(400, "Checkout request ID and address are required.");

        }

        var address = request.ShippingAddress.Normalize();

        var json = JsonSerializer.Serialize(address);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        await using (var tx = await BeginTransactionAsync(customer, ct))
        {
            var existing = await FindCheckoutAsync(request.RequestId, ct);

            if (existing is not null)
            {
                if (existing.CustomerId != customer || existing.AddressHash != hash)
                {
                    throw new ServiceException(409, "Checkout request ID was used for different content.");

                }

                if (existing.Status == CheckoutStatus.Failed)
                {
                    throw new ServiceException(409, "Checkout failed. Edit the cart and use a new request ID.");

                }

                await tx.CommitAsync(ct);

            }

            else
            {
                var cart = await GetOrCreate(customer, ct);

                cart.BeginCheckout(request.RequestId);

                var attempt = CheckoutAttempt.Create(request.RequestId, customer, json, hash, cart.Items);

                Add(attempt);

                await SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

            }

        }

        await using var execution = await BeginTransactionAsync(customer, ct);

        var current = await FindCheckoutAsync(
            request.RequestId,
            ct) ?? throw new ServiceException(
            409,
            "Checkout no longer exists.");

        if (current.Status == CheckoutStatus.Failed)
        {
            throw new ServiceException(409, "Checkout failed. Use a new request ID.");

        }

        if (current.Status == CheckoutStatus.Completed)
        {
            var prior = await orders.GetAsync(request.RequestId, ct);

            if (prior.CustomerId != customer)
            {
                throw new ServiceException(403, "Order ownership mismatch.");

            }

            await execution.CommitAsync(ct);

            return prior;
        }

        OrderSnapshot order;

        try
        {
            order = await orders.CreateAsync(request.RequestId, customer, address, current.Items, ct);

        }

        catch (ServiceException error)when (error.StatusCode is 400 or 409)
        {
            var failedCart = await FindAsync(customer, ct);

            failedCart?.AbandonCheckout(request.RequestId);

            current.Fail();

            await SaveChangesAsync(ct);

            await execution.CommitAsync(ct);

            throw;

        }

        if (order.CustomerId != customer || order.Id != request.RequestId)
        {
            throw new ServiceException(503, "Order returned an invalid checkout result.");

        }

        var cartToClear = await FindAsync(
            customer,
            ct) ?? throw new ServiceException(
            409,
            "Cart no longer exists.");

        cartToClear.CompleteCheckout(request.RequestId);

        current.Complete();

        await SaveChangesAsync(ct);

        await execution.CommitAsync(ct);

        return order;
    }

    private async Task<ShoppingCart> GetOrCreate(Guid id, CancellationToken ct)
    {
        var cart = await FindAsync(id, ct);

        if (cart is null)
        {
            cart = ShoppingCart.Create(id);

            Add(cart);

        }

        return cart;
    }

    public async Task<OrderSnapshot> RetryCheckoutAsync(Guid customer, CancellationToken ct)
    {
        var cart = await FindAsync(customer, ct);

        var id = cart?.PendingCheckoutId ?? throw new ServiceException(409, "No checkout is pending.");

        var attempt = await FindCheckoutAsync(
            id,
            ct) ?? throw new ServiceException(
            409,
            "Checkout snapshot is missing.");

        if (attempt.CustomerId != customer)
        {
            throw new ServiceException(404, "Checkout not found.");

        }

        var address = JsonSerializer.Deserialize<AddressRequest>(
            attempt.AddressJson) ?? throw new ServiceException(
            409,
            "Checkout address is missing.");

        return await CheckoutAsync(customer, new(id, address), ct);
    }

    protected virtual async Task<ICartTransaction> BeginTransactionAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var key = "cart:" + id;

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

    protected virtual Task<ShoppingCart?> FindAsync(
        Guid id,
        CancellationToken ct) => db.Carts.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.CustomerId == id,
        ct);

    protected virtual Task<CheckoutAttempt?> FindCheckoutAsync(
        Guid id,
        CancellationToken ct) => db.Checkouts.Include(
        x => x.Items).SingleOrDefaultAsync(
        x => x.RequestId == id,
        ct);

    protected virtual void Add(ShoppingCart cart) => db.Carts.Add(cart);

    protected virtual void Add(CheckoutAttempt attempt) => db.Checkouts.Add(attempt);

    protected virtual async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction tx) : ICartTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();

    }

}
