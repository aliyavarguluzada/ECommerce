using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryHandler(ICategoryRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(Guid id, UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetForUpdateAsync(id, cancellationToken);
        if (category is null) return false;

        category.UpdateDetails(command.Name, command.Description);
        if (await repository.NameExistsAsync(category.Name, id, cancellationToken))
            throw new DuplicateCategoryException();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
