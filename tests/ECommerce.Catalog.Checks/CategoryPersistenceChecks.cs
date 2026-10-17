using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using ECommerce.Catalog.Domain.Aggregates.Categories;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Domain.ValueObjects;
using ECommerce.Catalog.Application.Products;

internal static class CategoryPersistenceChecks
{
    public static async Task RunAsync()
    {
        var duplicate = await CaptureAsync("IX_categories_Name", PostgresErrorCodes.UniqueViolation);

        if (duplicate is not DuplicateCategoryException)
        {
            throw new InvalidOperationException("Category unique-index violation must become a category conflict.");
        }

        var otherConstraint = await CaptureAsync("PK_categories", PostgresErrorCodes.UniqueViolation);

        var otherError = await CaptureAsync("IX_categories_Name", PostgresErrorCodes.ForeignKeyViolation);

        if (otherConstraint is not DbUpdateException || otherError is not DbUpdateException)
        {
            throw new InvalidOperationException("Unrelated persistence failures must not become category-name conflicts.");
        }

        var deletionRace = await CaptureAsync(
            "FK_products_categories_CategoryId",
            PostgresErrorCodes.ForeignKeyViolation,
            deleteCategory: true);

        var nonDeletion = await CaptureAsync(
            "FK_products_categories_CategoryId",
            PostgresErrorCodes.ForeignKeyViolation);

        var concurrency = await CaptureAsync("unused", "unused", deleteCategory: true, concurrency: true);

        if (deletionRace is not CategoryInUseException || nonDeletion is not DbUpdateException || concurrency is not CategoryConcurrencyException)
        {
            throw new InvalidOperationException("Deletion FK and concurrency failures must be mapped selectively.");
        }

        var productCategoryRace = await CaptureAsync(
            "FK_products_categories_CategoryId",
            PostgresErrorCodes.ForeignKeyViolation,
            addProduct: true);

        var productConcurrency = await CaptureAsync("unused", "unused", concurrency: true, addProduct: true);

        if (productCategoryRace is not InvalidProductCategoryException || productConcurrency is not ProductConcurrencyException)
        {
            throw new InvalidOperationException("Product FK and concurrency failures must be mapped selectively.");
        }

        Console.WriteLine("PostgreSQL conflict translation checks passed (injected provider errors).");

    }

    private static async Task<Exception> CaptureAsync(
        string constraint,
        string sqlState,
        bool deleteCategory = false,
        bool concurrency = false,
        bool addProduct = false)
    {
        using var db = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql("Host=localhost;Database=checks;Username=checks;Password=checks").AddInterceptors(
                new FailureInterceptor(constraint, sqlState, concurrency)).Options);

        if (deleteCategory)
        {
            db.Categories.Remove(Category.Create("Delete candidate", null));

        }

        if (addProduct)
        {
            db.Products.Add(Product.Create("Insert candidate", null, new Money(1, "USD"), Guid.NewGuid()));

        }

        try
        {
            await db.SaveChangesAsync();

        }

        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected injected persistence failure.");
    }

    private sealed class FailureInterceptor(
        string constraint,
        string sqlState,
        bool concurrency) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context!.ChangeTracker.Entries().ToArray();

            if (concurrency)
            {
                throw new InjectedConcurrencyException(entries);
            }

            throw new DbUpdateException(
                "Injected persistence error",
                new PostgresException(
                    "Injected provider error",
                    "ERROR",
                    "ERROR",
                    sqlState,
                    constraintName: constraint),
                entries);
        }

    }

    private sealed class InjectedConcurrencyException(
        IReadOnlyList<EntityEntry> entries) : DbUpdateConcurrencyException("Injected concurrency failure")
    {
        public override IReadOnlyList<EntityEntry> Entries => entries;

    }

}
