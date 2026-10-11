namespace ECommerce.Cart.Domain;
public sealed class DomainConflictException(string message) : Exception(message);
