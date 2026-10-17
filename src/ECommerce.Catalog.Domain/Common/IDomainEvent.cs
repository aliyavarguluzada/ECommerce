namespace ECommerce.Catalog.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }

}
