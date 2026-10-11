namespace ECommerce.Catalog.Application.Categories;

public sealed class CategoryConcurrencyException()
    : Exception("The category changed or was deleted during this request. Reload it and try again.");
