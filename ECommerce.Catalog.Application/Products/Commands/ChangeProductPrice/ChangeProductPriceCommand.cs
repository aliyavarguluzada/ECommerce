using System.Text.Json.Serialization;
namespace ECommerce.Catalog.Application.Products.Commands.ChangeProductPrice;

public sealed record ChangeProductPriceCommand([property: JsonRequired] decimal Price, string Currency);
