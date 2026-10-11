namespace ECommerce.Payment.Domain;
public sealed class DomainException(string message) : Exception(message);
