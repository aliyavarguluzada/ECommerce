using System.Text.Json.Serialization;

namespace ECommerce.Catalog.Application.Products;

public sealed record CreateProductRequest(
    string Name,
    string? Description,
    [property: JsonRequired] decimal Price,
    string Currency,
    Guid CategoryId);
