using ECommerce.Catalog.Application.Abstractions;

namespace ECommerce.Catalog.Application.Products.Commands.DiscontinueProduct;

public sealed class DiscontinueProductHandler(IProductRepository products, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await products.GetForUpdateAsync(id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.Discontinue();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

}
