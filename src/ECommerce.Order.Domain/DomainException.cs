namespace ECommerce.Order.Domain;
public sealed class DomainException(string message) : Exception(message);
