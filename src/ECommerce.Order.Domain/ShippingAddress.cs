namespace ECommerce.Order.Domain;

public sealed record ShippingAddress
{
    public string Recipient { get; private set; } = null!;

    public string Line1 { get; private set; } = null!;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = null!;

    public string? Region { get; private set; }

    public string PostalCode { get; private set; } = null!;

    public string CountryCode { get; private set; } = null!;

    private ShippingAddress()
    {
    }

    public ShippingAddress(
        string recipient,
        string line1,
        string? line2,
        string city,
        string? region,
        string postalCode,
        string countryCode)
    {
        Recipient = Required(recipient, 150);

        Line1 = Required(line1, 200);

        Line2 = Optional(line2, 200);

        City = Required(city, 100);

        Region = Optional(region, 100);

        PostalCode = Required(postalCode, 20);

        CountryCode = Required(countryCode, 2).ToUpperInvariant();

        if (CountryCode.Length != 2 || CountryCode.Any(c => c is < 'A' or > 'Z'))
        {
            throw new DomainException("Country code must contain two letters.");

        }

    }

    private static string Required(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > limit)
        {
            throw new DomainException($"Address field is required and cannot exceed {limit} characters.");

        }

        return value.Trim();
    }

    private static string? Optional(string? value, int limit)
    {
        if (value?.Trim().Length > limit)
        {
            throw new DomainException($"Address field cannot exceed {limit} characters.");

        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
