using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace ECommerce.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_categories_Name" })
        {
            // The unique index handles simultaneous requests that pass the pre-check.
            throw new DuplicateCategoryException();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_products_categories_CategoryId" }
            && exception.Entries.Any(x => x.Entity is Category && x.State == EntityState.Deleted))
        {
            // A product may be added after the application checks for existing products.
            throw new CategoryInUseException();
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Category))
        {
            throw new CategoryConcurrencyException();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_products_categories_CategoryId" }
            && exception.Entries.Any(x => x.Entity is Product && x.State is EntityState.Added or EntityState.Modified))
        {
            throw new InvalidProductCategoryException();
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Product))
        {
            throw new ProductConcurrencyException();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
