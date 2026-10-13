using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Domain.ValueObjects;
namespace ECommerce.Catalog.Application.Products.Commands.CreateProduct;

public sealed class CreateProductHandler(
    IProductRepository products, ICategoryRepository categories, IUnitOfWork unitOfWork)
{
    public async Task<ProductResponse> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = Product.Create(command.Name, command.Description, new Money(command.Price, command.Currency), command.CategoryId);
        var category = await categories.GetByIdAsync(command.CategoryId, cancellationToken)
            ?? throw new InvalidProductCategoryException();
        await products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProductResponse(product.Id, product.Name, product.Description, product.Price.Amount,
            product.Price.Currency, product.CategoryId, category.Name, product.IsActive, product.CreatedAt, product.UpdatedAt);
    }
}
