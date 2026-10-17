namespace ECommerce.Order.Domain;

public sealed class OrderLine
{
    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine()
    {
    }

    public OrderLine(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty || string.IsNullOrWhiteSpace(productName) || productName.Length > 200 || quantity is < 1 or > 100000 || unitPrice < 0 || unitPrice > 9999999999999999.99m || decimal.Round(
            unitPrice,
            2) != unitPrice || unitPrice * quantity > 9999999999999999.99m)
        {
            throw new DomainException("Invalid order line.");

        }

        ProductId = productId;

        ProductName = productName;

        Quantity = quantity;

        UnitPrice = unitPrice;

    }

}
