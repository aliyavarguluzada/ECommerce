using ECommerce.Catalog.Domain.Common;

namespace ECommerce.Catalog.Domain.Aggregates.Categories;

public sealed class Category : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Category() { }

    private Category(string name, string? description) : base(Guid.NewGuid())
    {
        Name = DomainRules.Name(name, 150);
        Description = DomainRules.Description(description, 1000);
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public static Category Create(string name, string? description) => new(name, description);

    public void Rename(string name)
    {
        Name = DomainRules.Name(name, 150);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description)
    {
        var validName = DomainRules.Name(name, 150);
        var validDescription = DomainRules.Description(description, 1000);
        Name = validName;
        Description = validDescription;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = DomainRules.Description(description, 1000);
        UpdatedAt = DateTime.UtcNow;
    }
}
