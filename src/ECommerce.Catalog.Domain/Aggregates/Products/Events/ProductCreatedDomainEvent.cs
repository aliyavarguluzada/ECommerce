using ECommerce.Catalog.Domain.Common;

namespace ECommerce.Catalog.Domain.Aggregates.Products.Events;

public sealed record ProductCreatedDomainEvent(Guid ProductId, string Name, DateTime OccurredAt) : IDomainEvent;
