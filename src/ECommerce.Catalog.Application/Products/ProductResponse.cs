namespace ECommerce.Catalog.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string CategoryName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public ProductResponse() : this(
        Guid.Empty,
        string.Empty,
        null,
        0,
        string.Empty,
        Guid.Empty,
        string.Empty,
        false,
        default,
        default)
    {
    }

}
