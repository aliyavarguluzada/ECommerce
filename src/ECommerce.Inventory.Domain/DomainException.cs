namespace ECommerce.Inventory.Domain;
public sealed class DomainException(string message) : Exception(message);
