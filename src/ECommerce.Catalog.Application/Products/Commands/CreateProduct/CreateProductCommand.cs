using System.Text.Json.Serialization;

namespace ECommerce.Catalog.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string? Description,
    [property: JsonRequired] decimal Price,
    string Currency,
    Guid CategoryId);
