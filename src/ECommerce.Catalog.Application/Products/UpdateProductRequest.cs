using System.Text.Json.Serialization;

namespace ECommerce.Catalog.Application.Products;

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    [property: JsonRequired] decimal Price,
    string Currency,
    Guid CategoryId);
