using ECommerce.Catalog.Domain.Aggregates.Categories;

namespace ECommerce.Catalog.Application.Abstractions;

public interface ICategoryRepository
{
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasProductsAsync(Guid id, CancellationToken cancellationToken = default);

    void Remove(Category category);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

}
