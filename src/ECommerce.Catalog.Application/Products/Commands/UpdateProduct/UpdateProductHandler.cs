using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.ValueObjects;

namespace ECommerce.Catalog.Application.Products.Commands.UpdateProduct;

public sealed class UpdateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(
        Guid id,
        UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var product = await products.GetForUpdateAsync(id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.UpdateDetails(
            command.Name,
            command.Description,
            new Money(command.Price, command.Currency),
            command.CategoryId);

        if (await categories.GetByIdAsync(command.CategoryId, cancellationToken)is null)
        {
            throw new InvalidProductCategoryException();

        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

}
