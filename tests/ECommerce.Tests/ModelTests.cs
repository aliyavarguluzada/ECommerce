using ECommerce.Catalog.Infrastructure.Persistence;
using ECommerce.Inventory.Infrastructure;
using ECommerce.Order.Infrastructure;
using ECommerce.Identity.Infrastructure;
using ECommerce.Cart.Infrastructure;
using ECommerce.Payment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerce.Tests;
public sealed class ModelTests
{
    [Theory]
    [InlineData("Catalog")] [InlineData("Inventory")] [InlineData("Order")]
    [InlineData("Identity")] [InlineData("Cart")] [InlineData("Payment")]
    public void PostgreSQL_models_match_migrations_and_generate_complete_scripts(string service)
    {
        using var db = Create(service);
        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE", script);
        Assert.Contains("__EFMigrationsHistory", script);
        Assert.NotEmpty(db.Model.GetEntityTypes());
    }
    private static DbContext Create(string service) => service switch
    {
        "Catalog" => new CatalogDbContext(Options<CatalogDbContext>()),
        "Inventory" => new InventoryDbContext(Options<InventoryDbContext>()),
        "Order" => new OrderDbContext(Options<OrderDbContext>()),
        "Identity" => new IdentityDbContext(Options<IdentityDbContext>()),
        "Cart" => new CartDbContext(Options<CartDbContext>()),
        "Payment" => new PaymentDbContext(Options<PaymentDbContext>()),
        _ => throw new ArgumentException(service)
    };
    private static DbContextOptions<T> Options<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql("Host=localhost;Database=unused_model_check;Username=unused").Options;
}
