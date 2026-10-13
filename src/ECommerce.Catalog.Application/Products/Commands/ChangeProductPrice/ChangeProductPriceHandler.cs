using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Domain.ValueObjects;
namespace ECommerce.Catalog.Application.Products.Commands.ChangeProductPrice;

public sealed class ChangeProductPriceHandler(IProductRepository products, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(Guid id, ChangeProductPriceCommand command, CancellationToken cancellationToken = default)
    {
        var product = await products.GetForUpdateAsync(id, cancellationToken);
        if (product is null) return false;
        product.ChangePrice(new Money(command.Price, command.Currency));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
