using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Categories.Commands.DeleteCategory;

public sealed class DeleteCategoryHandler(ICategoryRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetForUpdateAsync(id, cancellationToken);
        if (category is null) return false;
        if (await repository.HasProductsAsync(id, cancellationToken))
            throw new CategoryInUseException();

        repository.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
