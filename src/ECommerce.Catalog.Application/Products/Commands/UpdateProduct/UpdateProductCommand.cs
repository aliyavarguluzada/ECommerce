using System.Text.Json.Serialization;

namespace ECommerce.Catalog.Application.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    string Name,
    string? Description,
    [property: JsonRequired] decimal Price,
    string Currency,
    Guid CategoryId);
