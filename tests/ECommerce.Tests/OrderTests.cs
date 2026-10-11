using ECommerce.Order.Application;
using ECommerce.Order.Domain;
using ECommerce.Tests.Support;
namespace ECommerce.Tests;

public sealed class OrderTests
{
    [Fact]
    public async Task Order_uses_catalog_prices_and_creation_replay_does_not_reserve_twice()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(price: 12.50m); var request = world.NewOrder(product, quantity: 3);
        var order = await world.Order.CreateAsync(request, default);
        world.Products[product] = world.Products[product] with { Price = 100 };
        var replay = await world.Order.CreateAsync(request, default);
        Assert.Equal(37.50m, replay.Total); Assert.Equal(order.Id, replay.Id);
        Assert.Equal(3, world.InventoryStore.Data.Stocks[product].Reserved);
        Assert.Single(world.OrderStore.Data.Orders);
    }
    [Fact]
    public async Task Changed_creation_content_cannot_reuse_the_order_id()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var request = world.NewOrder(product);
        await world.Order.CreateAsync(request, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<ServiceException>(() =>
            world.Order.CreateAsync(request with { CustomerId = Guid.NewGuid() }, default))).StatusCode);
    }
    [Fact]
    public async Task Lost_reserve_response_can_be_recovered_without_a_second_hold()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var request = world.NewOrder(product);
        world.InventoryClient.LoseNextReserveResponse = true;
        Assert.Equal(OrderStatus.PendingInventory, (await world.Order.CreateAsync(request, default)).Status);
        Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);
        Assert.Equal(OrderStatus.Reserved, (await world.Order.RetryAsync(request.RequestId, default)).Status);
        Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);
    }
    [Fact]
    public async Task Unpaid_order_cannot_confirm_or_ship()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await Assert.ThrowsAsync<DomainConflictException>(() => world.Order.ConfirmAsync(order.Id, default));
        await Assert.ThrowsAsync<DomainConflictException>(() => world.Order.ShipAsync(order.Id, new("Carrier", "TRACK"), default));
        Assert.Equal(OrderStatus.Reserved, world.OrderStore.Data.Orders[order.Id].Status);
    }
    [Fact]
    public async Task Paid_order_can_be_shipped_and_delivered_but_not_cancelled()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-approved"), default);
        await world.Order.ShipAsync(order.Id, new("Carrier", "TRACK"), default);
        await world.Order.DeliverAsync(order.Id, default); await world.Order.DeliverAsync(order.Id, default);
        Assert.Equal(OrderStatus.Delivered, world.OrderStore.Data.Orders[order.Id].Status);
        await Assert.ThrowsAsync<DomainConflictException>(() => world.Order.CancelAsync(order.Id, new("Cancel"), default));
        Assert.Equal(98, world.InventoryStore.Data.Stocks[product].OnHand);
    }
    [Fact]
    public async Task Cancellation_restores_stock_and_refunds_exactly_once()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-approved"), default);
        await world.Order.CancelAsync(order.Id, new("Customer request"), default);
        await world.Order.CancelAsync(order.Id, new("Customer request"), default);
        Assert.Equal(100, world.InventoryStore.Data.Stocks[product].OnHand); Assert.Equal(1, world.Provider.RefundCount);
        Assert.True(world.OrderStore.Data.Orders[order.Id].PaymentRefunded);
        Assert.Equal(OrderStatus.Cancelled, world.OrderStore.Data.Orders[order.Id].Status);
    }
    [Fact]
    public async Task Expired_unpaid_order_releases_hold()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await world.Order.RecoverAsync(order.Id, DateTime.UtcNow.AddMinutes(1), default);
        Assert.Equal(OrderStatus.Cancelled, world.OrderStore.Data.Orders[order.Id].Status);
        Assert.Equal(100, world.InventoryStore.Data.Stocks[product].Available);
    }
    [Fact]
    public async Task Currency_mismatch_rejects_creation_without_persisting_order()
    {
        var world = new CommerceWorld(); var a = world.AddProduct(); var b = world.AddProduct(currency: "EUR");
        var request = world.NewOrder(a) with { Items = [new(a, 1), new(b, 1)] };
        await Assert.ThrowsAsync<DomainException>(() => world.Order.CreateAsync(request, default));
        Assert.Empty(world.OrderStore.Data.Orders);
    }
    [Fact]
    public async Task Discontinued_product_rejects_creation()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(active: false);
        await Assert.ThrowsAsync<ServiceException>(() => world.Order.CreateAsync(world.NewOrder(product), default));
        Assert.Empty(world.OrderStore.Data.Orders);
    }
    [Fact]
    public async Task Wrong_payment_amount_does_not_mark_order_paid()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await Assert.ThrowsAsync<DomainConflictException>(() => world.Order.RecordPaymentAsync(order.Id, new(Guid.NewGuid(), order.Total + 1, "USD"), default));
        Assert.Null(world.OrderStore.Data.Orders[order.Id].PaymentId);
    }
}
