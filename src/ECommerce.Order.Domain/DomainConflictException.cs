namespace ECommerce.Order.Domain;

public sealed class DomainConflictException(string message) : Exception(message);
