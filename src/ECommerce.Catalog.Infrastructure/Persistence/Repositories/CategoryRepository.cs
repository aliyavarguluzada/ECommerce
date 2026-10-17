using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.Aggregates.Categories;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(CatalogDbContext db) : ICategoryRepository
{
    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default) => db.Categories.AnyAsync(
        x => x.Name == name && (!excludingId.HasValue || x.Id != excludingId.Value),
        cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(
        CancellationToken cancellationToken = default) => await db.Categories.AsNoTracking().OrderBy(
        x => x.Name).ThenBy(
        x => x.Id).ToListAsync(cancellationToken);

    public Task<Category?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Categories.SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    public Task<bool> HasProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Products.AnyAsync(
        x => x.CategoryId == id,
        cancellationToken);

    public void Remove(Category category) => db.Categories.Remove(category);

    public Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) => db.Categories.AsNoTracking().SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default) => await db.Categories.AddAsync(
        category,
        cancellationToken);

}
