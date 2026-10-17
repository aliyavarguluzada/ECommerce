using ECommerce.Catalog.Domain.Exceptions;

namespace ECommerce.Catalog.Domain.Common;

internal static class DomainRules
{
    internal static string Name(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Name is required.");
        }

        var name = value.Trim();

        if (name.Length > maxLength)
        {
            throw new DomainException($"Name cannot exceed {maxLength} characters.");
        }

        return name;
    }

    internal static string? Description(string? value, int maxLength)
    {
        var description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (description?.Length > maxLength)
        {
            throw new DomainException($"Description cannot exceed {maxLength} characters.");
        }

        return description;
    }

}
