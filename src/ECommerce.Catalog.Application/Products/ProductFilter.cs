using ECommerce.Catalog.Domain.Exceptions;

namespace ECommerce.Catalog.Application.Products;

public sealed record ProductFilter
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public Guid? CategoryId { get; init; }

    public string? Search { get; init; }

    public bool? IsActive { get; init; }

    public bool IncludeInactive { get; init; }

    public void Validate()
    {
        if (PageNumber < 1 || PageSize is < 1 or > 100 || (long)(PageNumber - 1) * PageSize > int.MaxValue)
        {
            throw new DomainException("Page number must be positive, page size must be 1–100, and the page offset must fit a 32-bit integer.");
        }

        if (CategoryId == Guid.Empty)
        {
            throw new DomainException("Category ID cannot be empty.");
        }

        if (Search?.Trim().Length > 200)
        {
            throw new DomainException("Search cannot exceed 200 characters.");
        }

    }

}
