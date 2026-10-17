using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.Aggregates.Categories;

namespace ECommerce.Catalog.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryHandler(ICategoryRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<CategoryResponse> HandleAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        var category = Category.Create(command.Name, command.Description);

        if (await repository.NameExistsAsync(category.Name, cancellationToken: cancellationToken))
        {
            throw new DuplicateCategoryException();

        }

        await repository.AddAsync(category, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

}
