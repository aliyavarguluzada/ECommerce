using System.Text.Json.Serialization;

namespace ECommerce.Catalog.Application.Products;

public sealed record ChangeProductPriceRequest([property: JsonRequired] decimal Price, string Currency);
