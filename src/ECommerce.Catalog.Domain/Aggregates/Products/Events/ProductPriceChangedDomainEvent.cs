using ECommerce.Catalog.Domain.Common;
using ECommerce.Catalog.Domain.ValueObjects;

namespace ECommerce.Catalog.Domain.Aggregates.Products.Events;

public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    Money PreviousPrice,
    Money NewPrice,
    DateTime OccurredAt) : IDomainEvent;
