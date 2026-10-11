using ECommerce.Payment.Application;
using ECommerce.Payment.Domain;
using ECommerce.Payment.Infrastructure;
using ECommerce.Tests.Support;
namespace ECommerce.Tests;
public sealed class PaymentTests
{
    [Fact]
    public async Task Successful_payment_confirms_order_and_replay_charges_once()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        var request = new PayCommand(Guid.NewGuid(), order.Id, "dev-approved");
        var receipt = await world.Payment.PayAsync(TestCredentials.Customer, request, default);
        await world.Payment.PayAsync(TestCredentials.Customer, request, default);
        Assert.Equal(PaymentStatus.Succeeded, receipt.Status); Assert.True(receipt.OrderNotified);
        Assert.Equal(ECommerce.Order.Domain.OrderStatus.Confirmed, world.OrderStore.Data.Orders[order.Id].Status);
        Assert.Equal(1, world.Provider.ChargeCount); Assert.Equal(98, world.InventoryStore.Data.Stocks[product].OnHand);
    }
    [Fact]
    public async Task Payment_customer_must_own_order()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        Assert.Equal(404, (await Assert.ThrowsAsync<ServiceException>(() => world.Payment.PayAsync(Guid.NewGuid(),
            new(Guid.NewGuid(), order.Id, "dev-approved"), default))).StatusCode);
        Assert.Equal(0, world.Provider.ChargeCount);
    }
    [Fact]
    public async Task Changed_method_cannot_reuse_request_id()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        var request = new PayCommand(Guid.NewGuid(), order.Id, "dev-approved");
        await world.Payment.PayAsync(TestCredentials.Customer, request, default);
        await Assert.ThrowsAsync<ServiceException>(() => world.Payment.PayAsync(TestCredentials.Customer, request with { MethodToken = "dev-declined" }, default));
        Assert.Equal(1, world.Provider.ChargeCount);
    }
    [Fact]
    public async Task Decline_leaves_hold_and_allows_new_attempt()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        var failed = await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-declined"), default);
        Assert.Equal(PaymentStatus.Failed, failed.Status); Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);
        Assert.Equal(PaymentStatus.Succeeded, (await world.Payment.PayAsync(TestCredentials.Customer,
            new(Guid.NewGuid(), order.Id, "dev-approved"), default)).Status);
        Assert.Equal(2, world.PaymentStore.Data.Payments.Count);
    }
    [Fact]
    public async Task Missing_callback_response_recovers_without_recharging_or_reconsuming_stock()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        world.PaymentOrderClient.LoseNextNotificationResponse = true;
        var receipt = await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-approved"), default);
        Assert.False(receipt.OrderNotified);
        await world.Payment.RecoverAsync(receipt.Id, default);
        Assert.True(world.PaymentStore.Data.Payments[receipt.Id].OrderNotified);
        Assert.Equal(1, world.Provider.ChargeCount); Assert.Equal(98, world.InventoryStore.Data.Stocks[product].OnHand);
    }
    [Fact]
    public async Task Late_success_after_cancellation_is_refunded()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        world.PaymentOrderClient.NotificationUnavailable = true;
        var receipt = await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-approved"), default);
        await world.Order.CancelAsync(order.Id, new("Cancel before callback"), default);
        world.PaymentOrderClient.NotificationUnavailable = false;
        await world.Payment.RecoverAsync(receipt.Id, default);
        Assert.Equal(PaymentStatus.Refunded, world.PaymentStore.Data.Payments[receipt.Id].Status);
        Assert.Equal(1, world.Provider.RefundCount); Assert.Equal(100, world.InventoryStore.Data.Stocks[product].Available);
    }
    [Fact]
    public async Task Refund_failure_keeps_order_cancelling_until_recovered()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        await world.Payment.PayAsync(TestCredentials.Customer, new(Guid.NewGuid(), order.Id, "dev-approved"), default);
        world.Provider.Unavailable = true;
        await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(() => world.Order.CancelAsync(order.Id, new("Refund outage"), default));
        Assert.Equal(ECommerce.Order.Domain.OrderStatus.Cancelling, world.OrderStore.Data.Orders[order.Id].Status);
        world.Provider.Unavailable = false;
        await world.Order.RetryAsync(order.Id, default);
        Assert.Equal(ECommerce.Order.Domain.OrderStatus.Cancelled, world.OrderStore.Data.Orders[order.Id].Status);
        Assert.Equal(1, world.Provider.RefundCount); Assert.Equal(100, world.InventoryStore.Data.Stocks[product].OnHand);
    }
    [Fact]
    public async Task Provider_outage_preserves_pending_receipt_for_retry()
    {
        var world = new CommerceWorld(); var product = world.AddProduct(); var order = await world.Order.CreateAsync(world.NewOrder(product), default);
        var request = new PayCommand(Guid.NewGuid(), order.Id, "dev-approved"); world.Provider.Unavailable = true;
        await Assert.ThrowsAsync<ServiceException>(() => world.Payment.PayAsync(TestCredentials.Customer, request, default));
        Assert.Equal(PaymentStatus.Pending, world.PaymentStore.Data.Payments[request.RequestId].Status);
        world.Provider.Unavailable = false;
        await world.Payment.RecoverAsync(request.RequestId, default);
        Assert.Equal(PaymentStatus.Succeeded, world.PaymentStore.Data.Payments[request.RequestId].Status);
    }
    [Fact]
    public async Task Development_provider_is_disabled_outside_development()
    {
        var provider = new DevelopmentPaymentProvider(false);
        Assert.Equal(503, (await Assert.ThrowsAsync<ServiceException>(() => provider.ChargeAsync(Guid.NewGuid(), 10, "USD", "dev-approved", default))).StatusCode);
    }
    [Theory] [InlineData("4242424242424242")] [InlineData("real-card")] [InlineData("")]
    public void Real_card_data_is_not_accepted_by_development_contract(string token) =>
        Assert.Throws<DomainException>(() => PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, "USD", token));
}
