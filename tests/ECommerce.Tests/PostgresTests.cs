using ECommerce.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Cat = ECommerce.Catalog.Infrastructure.Persistence;
using Inv = ECommerce.Inventory.Infrastructure;
using Ord = ECommerce.Order.Infrastructure;
using Car = ECommerce.Cart.Infrastructure;
using Pay = ECommerce.Payment.Infrastructure;
using Id = ECommerce.Identity.Infrastructure;

namespace ECommerce.Tests;

[Trait("Category", "PostgreSQL")]

public sealed class PostgresTests
{
    [PostgresFact]

    public async Task Catalog_persists_owned_money_and_detects_concurrent_updates()
    {
        await using var database = await PostgresDatabase.CreateAsync();

        await using var db = new Cat.CatalogDbContext(database.Options<Cat.CatalogDbContext>());

        await db.Database.MigrateAsync();

        var category = ECommerce.Catalog.Domain.Aggregates.Categories.Category.Create("Electronics", null);

        var product = ECommerce.Catalog.Domain.Aggregates.Products.Product.Create(
            "Mouse",
            null,
            new ECommerce.Catalog.Domain.ValueObjects.Money(12.50m, "usd"),
            category.Id);

        db.Categories.Add(category);

        db.Products.Add(product);

        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var first = await db.Products.SingleAsync();

        Assert.Equal(12.50m, first.Price.Amount);

        Assert.Equal("USD", first.Price.Currency);

        await using var competing = new Cat.CatalogDbContext(database.Options<Cat.CatalogDbContext>());

        var stale = await competing.Products.SingleAsync();

        first.Rename("Updated mouse");

        await db.SaveChangesAsync();

        stale.Rename("Stale mouse");

        await Assert.ThrowsAsync<ECommerce.Catalog.Application.Products.ProductConcurrencyException>(
            () => competing.SaveChangesAsync());

    }

    [PostgresFact]

    public async Task Inventory_serializes_competing_reservations_without_overselling()
    {
        await using var database = await PostgresDatabase.CreateAsync();

        var world = new CommerceWorld();

        var product = world.AddProduct(stock: 5);

        await using (var db = new Inv.InventoryDbContext(database.Options<Inv.InventoryDbContext>()))
        {
            await db.Database.MigrateAsync();

            await new ECommerce.Inventory.Infrastructure.InventoryService(
                db,
                world.Catalog).CreateAsync(
                new(product, 5),
                default);

        }

        async Task<bool> Reserve(Guid order)
        {
            await using var db = new Inv.InventoryDbContext(database.Options<Inv.InventoryDbContext>());

            try
            {
                await new ECommerce.Inventory.Infrastructure.InventoryService(
                    db,
                    world.Catalog).ReserveAsync(
                    new(order, [new(product, 4)]),
                    default);

                return true;
            }

            catch (ECommerce.Inventory.Application.ServiceException error)when (error.StatusCode == 409)
            {
                return false;
            }

        }

        var outcomes = await Task.WhenAll(Reserve(Guid.NewGuid()), Reserve(Guid.NewGuid()));

        Assert.Single(outcomes, x => x);

        await using var verify = new Inv.InventoryDbContext(database.Options<Inv.InventoryDbContext>());

        var stock = await verify.Stocks.SingleAsync();

        Assert.Equal(5, stock.OnHand);

        Assert.Equal(4, stock.Reserved);

        Assert.Equal(1, await verify.Reservations.CountAsync());

        var service = new ECommerce.Inventory.Infrastructure.InventoryService(
            verify,
            world.Catalog);

        var reservation = await verify.Reservations.SingleAsync();

        await service.ReleaseAsync(reservation.OrderId, default);

        Assert.Equal(5, (await service.GetAsync(product, default)).Available);

    }

    [PostgresFact]

    public async Task Order_persists_snapshots_and_replays_after_context_restart()
    {
        await using var database = await PostgresDatabase.CreateAsync();

        var world = new CommerceWorld();

        var product = world.AddProduct();

        var command = world.NewOrder(product);

        await using (var db = new Ord.OrderDbContext(database.Options<Ord.OrderDbContext>()))
        {
            await db.Database.MigrateAsync();

            var service = new ECommerce.Order.Infrastructure.OrderService(
                db,
                world.Catalog,
                world.InventoryClient,
                new RefundAdapter { Payment = world.Payment });

            await service.CreateAsync(command, default);

        }

        await using var restarted = new Ord.OrderDbContext(database.Options<Ord.OrderDbContext>());

        var fresh = new ECommerce.Order.Infrastructure.OrderService(
            restarted,
            world.Catalog,
            world.InventoryClient,
            new RefundAdapter { Payment = world.Payment });

        var replay = await fresh.CreateAsync(command, default);

        Assert.Equal(command.RequestId, replay.Id);

        Assert.Equal(20m, replay.Total);

        Assert.Single(replay.Items);

        Assert.Equal(1, await restarted.Orders.CountAsync());

        Assert.Equal(2, world.InventoryStore.Data.Stocks[product].Reserved);

        Assert.Equal(1, (await fresh.ListAsync(1, 20, command.CustomerId, null, default)).TotalCount);

    }

    [PostgresFact]

    public async Task Cart_persists_checkout_and_replay_preserves_new_contents()
    {
        await using var database = await PostgresDatabase.CreateAsync();

        var world = new CommerceWorld();

        var product = world.AddProduct();

        var customer = TestCredentials.Customer;

        var checkout = new ECommerce.Cart.Application.CheckoutRequest(Guid.NewGuid(), CommerceWorld.CartAddress());

        await using (var db = new Car.CartDbContext(database.Options<Car.CartDbContext>()))
        {
            await db.Database.MigrateAsync();

            var service = new ECommerce.Cart.Infrastructure.CartService(
                db,
                world.Catalog,
                world.CartOrderClient);

            await service.SetAsync(customer, product, 2, default);

            await service.CheckoutAsync(customer, checkout, default);

        }

        await using var restarted = new Car.CartDbContext(database.Options<Car.CartDbContext>());

        var fresh = new ECommerce.Cart.Infrastructure.CartService(
            restarted,
            world.Catalog,
            world.CartOrderClient);

        await fresh.SetAsync(customer, product, 3, default);

        await fresh.CheckoutAsync(customer, checkout, default);

        var cart = await fresh.GetAsync(customer, default);

        Assert.Equal(3, Assert.Single(cart.Items).Quantity);

        Assert.Equal(1, await restarted.Checkouts.CountAsync());

        Assert.Null(cart.PendingCheckoutId);

    }

    [PostgresFact]

    public async Task Payment_receipt_survives_restart_without_duplicate_charge_or_refund()
    {
        await using var database = await PostgresDatabase.CreateAsync();

        var world = new CommerceWorld();

        var product = world.AddProduct();

        var order = await world.Order.CreateAsync(world.NewOrder(product), default);

        var command = new ECommerce.Payment.Application.PayRequest(Guid.NewGuid(), order.Id, "dev-approved");

        await using (var db = new Pay.PaymentDbContext(database.Options<Pay.PaymentDbContext>()))
        {
            await db.Database.MigrateAsync();

            await new ECommerce.Payment.Infrastructure.PaymentService(
                db,
                world.PaymentOrderClient,
                world.Provider).PayAsync(
                TestCredentials.Customer,
                command,
                default);

        }

        await using var restarted = new Pay.PaymentDbContext(database.Options<Pay.PaymentDbContext>());

        var fresh = new ECommerce.Payment.Infrastructure.PaymentService(
            restarted,
            world.PaymentOrderClient,
            world.Provider);

        var receipt = await fresh.PayAsync(TestCredentials.Customer, command, default);

        Assert.True(receipt.OrderNotified);

        Assert.Equal(1, world.Provider.ChargeCount);

        await fresh.RefundOrderAsync(order.Id, default);

        await fresh.RefundOrderAsync(order.Id, default);

        Assert.Equal(1, world.Provider.RefundCount);

        Assert.Equal(1, await restarted.Payments.CountAsync());

    }

    [PostgresFact]

    public async Task Identity_rotates_tokens_revokes_reused_families_and_enforces_passwords_roles_and_lockout()
    {
        TestCredentials.Initialize();

        await using var database = await PostgresDatabase.CreateAsync();

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddHttpContextAccessor();

        services.AddAuthentication();

        services.AddDataProtection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddEnvironmentVariables().Build());

        Id.DependencyInjection.AddInfrastructure(services, database.Connection);

        await using var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<Id.IdentityDbContext>();

        await db.Database.MigrateAsync();

        await Id.DependencyInjection.SeedRolesAsync(scope.ServiceProvider);

        var service = scope.ServiceProvider.GetRequiredService<ECommerce.Identity.Application.IIdentityService>();

        const string password = "Password123!";

        var initial = await service.RegisterAsync(new("customer@example.com", password, "Customer"), default);

        Assert.Contains("Customer", initial.User.Roles);

        Assert.DoesNotContain("Admin", initial.User.Roles);

        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
                () => service.RegisterAsync(new("customer@example.com", password, "Duplicate"), default))).StatusCode);

        var rotated = await service.RefreshAsync(initial.RefreshToken, default);

        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.RefreshAsync(initial.RefreshToken, default));

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.RefreshAsync(rotated.RefreshToken, default));

        var login = await service.LoginAsync(new(initial.User.Email, password), default);

        await service.LogoutAsync(initial.User.Id, login.RefreshToken, default);

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.RefreshAsync(login.RefreshToken, default));

        var passwordSession = await service.LoginAsync(new(initial.User.Email, password), default);

        await service.ChangePasswordAsync(initial.User.Id, new(password, "ChangedPassword123!"), default);

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.RefreshAsync(passwordSession.RefreshToken, default));

        var newLogin = await service.LoginAsync(new(initial.User.Email, "ChangedPassword123!"), default);

        var promoted = await service.SetAdminAsync(Guid.NewGuid(), initial.User.Id, true, default);

        Assert.Contains("Admin", promoted.Roles);

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.RefreshAsync(newLogin.RefreshToken, default));

        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
                () => service.SetAdminAsync(initial.User.Id, initial.User.Id, false, default))).StatusCode);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
                () => service.LoginAsync(new(initial.User.Email, "WrongPassword123!"), default));

        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<Id.ApplicationUser>>();

        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(initial.User.Id.ToString()))!));

        await Assert.ThrowsAsync<ECommerce.Identity.Application.ServiceException>(
            () => service.LoginAsync(new(initial.User.Email, "ChangedPassword123!"), default));

    }

}
