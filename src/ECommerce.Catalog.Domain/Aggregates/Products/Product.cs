using ECommerce.Catalog.Domain.Aggregates.Products.Events;
using ECommerce.Catalog.Domain.Common;
using ECommerce.Catalog.Domain.Exceptions;
using ECommerce.Catalog.Domain.ValueObjects;

namespace ECommerce.Catalog.Domain.Aggregates.Products;

public sealed class Product : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Money Price { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Product() { }

    private Product(string name, string? description, Money price, Guid categoryId)
        : base(Guid.NewGuid())
    {
        Name = DomainRules.Name(name, 200);
        Description = DomainRules.Description(description, 2000);
        ArgumentNullException.ThrowIfNull(price);
        EnsureCategoryId(categoryId);
        Price = price;
        CategoryId = categoryId;
        IsActive = true;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public static Product Create(string name, string? description, Money price, Guid categoryId)
    {
        var product = new Product(name, description, price, categoryId);
        product.RaiseDomainEvent(new ProductCreatedDomainEvent(product.Id, product.Name, product.CreatedAt));
        return product;
    }

    public void Rename(string name)
    {
        Name = DomainRules.Name(name, 200);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, Money price, Guid categoryId)
    {
        var validName = DomainRules.Name(name, 200);
        var validDescription = DomainRules.Description(description, 2000);
        ArgumentNullException.ThrowIfNull(price);
        EnsureCategoryId(categoryId);
        if (Price.Currency != price.Currency)
            throw new DomainException("A product's currency cannot be changed.");

        ChangePrice(price);
        Name = validName;
        Description = validDescription;
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = DomainRules.Description(description, 2000);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        if (Price.Currency != newPrice.Currency)
            throw new DomainException("A product's currency cannot be changed.");

        if (Price == newPrice)
            return;

        var previousPrice = Price;
        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProductPriceChangedDomainEvent(Id, previousPrice, Price, UpdatedAt));
    }

    public void ChangeCategory(Guid categoryId)
    {
        EnsureCategoryId(categoryId);
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Discontinue()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void EnsureCategoryId(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            throw new DomainException("Category ID cannot be empty.");
    }
}
