namespace ECommerce.Catalog.Application.Categories;

public sealed class CategoryInUseException()
    : Exception("This category contains products and cannot be deleted.");
