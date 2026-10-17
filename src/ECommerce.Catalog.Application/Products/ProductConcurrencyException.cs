namespace ECommerce.Catalog.Application.Products;

public sealed class ProductConcurrencyException() : Exception("The product changed during this request. Reload it and try again.");
