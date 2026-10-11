namespace ECommerce.Catalog.Application.Products;

public sealed class InvalidProductCategoryException()
    : Exception("The selected category does not exist. Select an existing category.");
