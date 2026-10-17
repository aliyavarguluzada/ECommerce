using ECommerce.Inventory.Domain;
using ECommerce.Inventory.Application;
using ECommerce.Tests.Support;

namespace ECommerce.Tests;

public sealed class InventoryTests
{
    [Theory]

    [InlineData(0)]

    [InlineData(-1)]

    [InlineData(100001)]

    public void Reservation_line_requires_bounded_positive_quantity(
        int quantity) => Assert.Throws<DomainException>(
        () => new ReservationLine(Guid.NewGuid(), quantity));

    [Fact]

    public void Stock_adjustment_preserves_reserved_units()
    {
        var stock = StockItem.Create(Guid.NewGuid(), 10);

        stock.Reserve(8);

        Assert.Throws<DomainException>(() => stock.Adjust(-3));

        Assert.Equal(10, stock.OnHand);

        Assert.Equal(8, stock.Reserved);

        Assert.Equal(2, stock.Available);

    }

    [Fact]

    public async Task Reserve_commit_and_release_are_idempotent()
    {
        var world = new CommerceWorld();

        var id = world.AddProduct(stock: 10);

        var order = Guid.NewGuid();

        var request = new ReserveStockRequest(order, [new(id, 3)]);

        await world.Inventory.ReserveAsync(request, default);

        await world.Inventory.ReserveAsync(request, default);

        Assert.Equal(3, world.InventoryStore.Data.Stocks[id].Reserved);

        await world.Inventory.CommitAsync(order, default);

        await world.Inventory.CommitAsync(order, default);

        Assert.Equal(7, world.InventoryStore.Data.Stocks[id].OnHand);

        Assert.Equal(0, world.InventoryStore.Data.Stocks[id].Reserved);

        await world.Inventory.ReleaseAsync(order, default);

        await world.Inventory.ReleaseAsync(order, default);

        Assert.Equal(10, world.InventoryStore.Data.Stocks[id].OnHand);

    }

    [Fact]

    public async Task Partial_bundle_failure_does_not_hold_any_stock()
    {
        var world = new CommerceWorld();

        var first = world.AddProduct(stock: 10);

        var second = world.AddProduct(stock: 0);

        var error = await Assert.ThrowsAsync<ServiceException>(
            () => world.Inventory.ReserveAsync(new(Guid.NewGuid(), [new(first, 2), new(second, 1)]), default));

        Assert.Equal(409, error.StatusCode);

        Assert.All(world.InventoryStore.Data.Stocks.Values, x => Assert.Equal(0, x.Reserved));

        Assert.Empty(world.InventoryStore.Data.Reservations);

    }

    [Fact]

    public async Task Release_unknown_order_prevents_a_late_reservation()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        var id = Guid.NewGuid();

        await world.Inventory.ReleaseAsync(id, default);

        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<ServiceException>(
                () => world.Inventory.ReserveAsync(new(id, [new(product, 1)]), default))).StatusCode);

        Assert.Equal(100, world.InventoryStore.Data.Stocks[product].Available);

    }

    [Fact]

    public async Task Changing_a_repeated_reservation_is_rejected()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        var id = Guid.NewGuid();

        await world.Inventory.ReserveAsync(new(id, [new(product, 2)]), default);

        await Assert.ThrowsAsync<ServiceException>(
            () => world.Inventory.ReserveAsync(new(id, [new(product, 3)]), default));

        Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);

    }

    [Fact]

    public async Task Adjustment_replay_does_not_change_balance_twice()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct(stock: 10);

        var id = Guid.NewGuid();

        await world.Inventory.AdjustAsync(product, new(id, 5, " Delivery "), default);

        await world.Inventory.AdjustAsync(product, new(id, 5, "Delivery"), default);

        Assert.Equal(15, world.InventoryStore.Data.Stocks[product].OnHand);

        await Assert.ThrowsAsync<ServiceException>(
            () => world.Inventory.AdjustAsync(product, new(id, 6, "Delivery"), default));

        Assert.Single(world.InventoryStore.Data.Adjustments);

    }

    [Fact]

    public async Task Releasing_uncommitted_stock_does_not_increase_on_hand()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct(stock: 10);

        var id = Guid.NewGuid();

        await world.Inventory.ReserveAsync(new(id, [new(product, 2)]), default);

        await world.Inventory.ReleaseAsync(id, default);

        Assert.Equal(10, world.InventoryStore.Data.Stocks[product].OnHand);

        Assert.Equal(0, world.InventoryStore.Data.Stocks[product].Reserved);

        await Assert.ThrowsAsync<ServiceException>(() => world.Inventory.CommitAsync(id, default));

    }

    [Theory]

    [InlineData(0, 20)]

    [InlineData(1, 0)]

    [InlineData(1, 101)]

    [InlineData(int.MaxValue, 100)]

    public async Task Invalid_paging_is_rejected(int page, int size)
    {
        var world = new CommerceWorld();

        await Assert.ThrowsAsync<DomainException>(() => world.Inventory.ListAsync(page, size, null, default));

    }

}
