using ECommerce.Cart.Application;
using ECommerce.Cart.Domain;
using ECommerce.Tests.Support;
namespace ECommerce.Tests;
public sealed class CartTests
{
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(100001)]
    public void Quantity_must_be_bounded_and_positive(int quantity)
    { var cart = ShoppingCart.Create(Guid.NewGuid()); Assert.Throws<DomainException>(() => cart.SetQuantity(Guid.NewGuid(), quantity)); Assert.Empty(cart.Items); }
    [Fact]
    public async Task Quote_reflects_current_catalog_prices()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(price: 10);
        await world.Cart.SetAsync(TestCredentials.Customer, product, 3, default);
        world.Products[product] = world.Products[product] with { Price = 12 };
        var quote = await world.Cart.GetAsync(TestCredentials.Customer, default);
        Assert.True(quote.CanCheckout); Assert.Equal(36, quote.Subtotal);
    }
    [Fact]
    public async Task Missing_or_discontinued_products_remain_visible_but_cannot_checkout()
    {
        var world = new CommerceWorld(); var product = world.AddProduct();
        await world.Cart.SetAsync(TestCredentials.Customer, product, 1, default);
        world.Products.Remove(product);
        var quote = await world.Cart.GetAsync(TestCredentials.Customer, default);
        Assert.False(quote.CanCheckout); Assert.False(quote.Items.Single().IsAvailable); Assert.Null(quote.Subtotal);
    }
    [Fact]
    public async Task Mixed_currency_quote_cannot_checkout()
    {
        var world = new CommerceWorld(); var a = world.AddProduct(); var b = world.AddProduct(currency: "EUR");
        await world.Cart.SetAsync(TestCredentials.Customer, a, 1, default); await world.Cart.SetAsync(TestCredentials.Customer, b, 1, default);
        Assert.False((await world.Cart.GetAsync(TestCredentials.Customer, default)).CanCheckout);
    }
    [Fact]
    public async Task Lost_checkout_response_is_recoverable_and_cart_is_frozen_until_retry()
    {
        var world = new CommerceWorld(); var product = world.AddProduct();
        await world.Cart.SetAsync(TestCredentials.Customer, product, 2, default);
        var request = new CheckoutCommand(Guid.NewGuid(), CommerceWorld.CartAddress());
        world.CartOrderClient.LoseNextCreateResponse = true;
        await Assert.ThrowsAsync<ServiceException>(() => world.Cart.CheckoutAsync(TestCredentials.Customer, request, default));
        Assert.Single(world.OrderStore.Data.Orders);
        Assert.Equal(request.RequestId, world.CartStore.Data.Carts[TestCredentials.Customer].PendingCheckoutId);
        await Assert.ThrowsAsync<DomainConflictException>(() => world.Cart.ClearAsync(TestCredentials.Customer, default));
        await world.Cart.RetryCheckoutAsync(TestCredentials.Customer, default);
        Assert.Empty(world.CartStore.Data.Carts[TestCredentials.Customer].Items);
        Assert.Single(world.OrderStore.Data.Orders);
        Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);
    }
    [Fact]
    public async Task Replaying_completed_checkout_does_not_clear_new_cart_items()
    {
        var world = new CommerceWorld(); var first = world.AddProduct(); var second = world.AddProduct();
        await world.Cart.SetAsync(TestCredentials.Customer, first, 2, default);
        var request = new CheckoutCommand(Guid.NewGuid(), CommerceWorld.CartAddress());
        await world.Cart.CheckoutAsync(TestCredentials.Customer, request, default);
        await world.Cart.SetAsync(TestCredentials.Customer, second, 3, default);
        var replay = await world.Cart.CheckoutAsync(TestCredentials.Customer, request, default);
        Assert.Equal(request.RequestId, replay.Id);
        Assert.Equal(second, world.CartStore.Data.Carts[TestCredentials.Customer].Items.Single().ProductId);
    }
    [Fact]
    public async Task Known_checkout_rejection_preserves_items_and_unfreezes_cart()
    {
        var world = new CommerceWorld(); var product = world.AddProduct();
        await world.Cart.SetAsync(TestCredentials.Customer, product, 2, default);
        world.CartOrderClient.RejectCreate = true;
        await Assert.ThrowsAsync<ServiceException>(() => world.Cart.CheckoutAsync(TestCredentials.Customer, new(Guid.NewGuid(), CommerceWorld.CartAddress()), default));
        var cart = world.CartStore.Data.Carts[TestCredentials.Customer];
        Assert.Null(cart.PendingCheckoutId); Assert.Single(cart.Items);
        await world.Cart.SetAsync(TestCredentials.Customer, product, 3, default);
    }
    [Fact]
    public async Task Address_change_cannot_reuse_a_checkout_request()
    {
        var world = new CommerceWorld(); var product = world.AddProduct();
        await world.Cart.SetAsync(TestCredentials.Customer, product, 1, default);
        var request = new CheckoutCommand(Guid.NewGuid(), CommerceWorld.CartAddress());
        await world.Cart.CheckoutAsync(TestCredentials.Customer, request, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<ServiceException>(() => world.Cart.CheckoutAsync(TestCredentials.Customer,
            request with { ShippingAddress = request.ShippingAddress with { City = "Changed" } }, default))).StatusCode);
    }
}
