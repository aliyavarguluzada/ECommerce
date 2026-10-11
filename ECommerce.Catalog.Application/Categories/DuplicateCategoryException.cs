namespace ECommerce.Catalog.Application.Categories;
public sealed class DuplicateCategoryException()
    : Exception("A category with this name already exists.");
