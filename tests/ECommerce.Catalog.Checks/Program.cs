using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Domain.Aggregates.Products.Events;
using ECommerce.Catalog.Domain.Exceptions;
using ECommerce.Catalog.Domain.ValueObjects;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

var category = Category.Create(" Electronics ", " Devices ");

Check(category.Name == "Electronics" && category.Description == "Devices", "Category normalization");

var product = Product.Create(" Mouse ", null, new Money(99.99m, " usd "), category.Id);

Check(product.Name == "Mouse" && product.Price.Currency == "USD", "Product normalization");

Check(product.Id != Guid.Empty && product.IsActive, "Product identity and lifecycle");

Check(product.CreatedAt.Kind == DateTimeKind.Utc && product.CreatedAt == product.UpdatedAt, "UTC timestamps");

Check(product.DomainEvents.Single() is ProductCreatedDomainEvent, "Creation event");

ExpectDomainError(() => new Money(-1, "USD"));

ExpectDomainError(() => new Money(1.001m, "USD"));

ExpectDomainError(() => new Money(10000000000000000m, "USD"));

ExpectDomainError(() => new Money(1, "12$"));

ExpectDomainError(() => Product.Create(" ", null, new Money(1, "USD"), category.Id));

ExpectDomainError(() => Product.Create("Mouse", null, new Money(1, "USD"), Guid.Empty));

ExpectDomainError(() => category.UpdateDescription(new string ('x', 1001)));

ExpectDomainError(() => product.UpdateDescription(new string ('x', 2001)));

ExpectDomainError(() => product.Rename(new string ('x', 201)));

ExpectDomainError(() => category.Rename(new string ('x', 151)));

product.ClearDomainEvents();

ExpectDomainError(() => product.ChangePrice(new Money(10, "EUR")));

Check(product.Price == new Money(99.99m, "USD") && product.DomainEvents.Count == 0, "Rejected price change is atomic");

product.ChangePrice(new Money(100, "USD"));

var priceEvent = (ProductPriceChangedDomainEvent)product.DomainEvents.Single();

Check(priceEvent.PreviousPrice.Amount == 99.99m && priceEvent.NewPrice.Amount == 100, "Price event values");

product.ChangePrice(new Money(100, "USD"));

Check(product.DomainEvents.Count == 1, "Unchanged price emits no event");

product.Discontinue();

Check(!product.IsActive && product.Id != Guid.Empty, "Discontinue preserves identity");

// Build the real PostgreSQL model without connecting to a database.

using var db = new CatalogDbContext(
    new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql("Host=localhost;Database=catalog_checks;Username=checks;Password=checks").Options);

var productType = db.Model.FindEntityType(typeof(Product))!;

var categoryType = db.Model.FindEntityType(typeof(Category))!;

Check(productType.FindProperty("DomainEvents")is null, "Domain events excluded from persistence");

Check(productType.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.Restrict, "Category deletion restricted");

Check(categoryType.GetIndexes().Single().IsUnique, "Category name uniqueness");

Check(productType.FindNavigation(nameof(Product.Price))!.TargetEntityType.IsOwned(), "Money owned mapping");

db.Products.Add(product);

Check(db.ChangeTracker.Entries().Count() == 2, "Aggregate and Money tracking");

Check(db.Database.GenerateCreateScript().Contains("currency"), "PostgreSQL schema generation");

Console.WriteLine("Catalog domain and PostgreSQL model checks passed.");

Check(!db.Database.HasPendingModelChanges(), "Migration snapshot matches the current model");

Check(
    productType.FindProperty(
        nameof(Product.UpdatedAt))!.IsConcurrencyToken && categoryType.FindProperty(
        nameof(Category.UpdatedAt))!.IsConcurrencyToken,
    "Aggregate concurrency protection");

var previousCategoryName = category.Name;

ExpectDomainError(() => category.UpdateDetails("Rejected name", new string ('x', 1001)));

Check(category.Name == previousCategoryName, "Category update validation is atomic");

var previousProductName = product.Name;

ExpectDomainError(() => product.UpdateDetails("Rejected name", null, new Money(10, "EUR"), category.Id));

Check(product.Name == previousProductName && product.Price.Amount == 100, "Product update validation is atomic");

await CategoryApiChecks.RunAsync();

await CategoryPersistenceChecks.RunAsync();

ProductQueryChecks.Run();

await ProductApiChecks.RunAsync();

await PostgreSqlIntegrationChecks.RunAsync();

static void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Failed: {name}");

    }

}

static void ExpectDomainError(Action action)
{
    try
    {
        action();

    }

    catch (DomainException)
    {
        return;
    }

    throw new InvalidOperationException("Expected a domain validation error.");

}
