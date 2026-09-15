namespace OrderFlow.Order.Domain.Orders;

public sealed class OrderLine
{
    private OrderLine()
    {
    }

    internal OrderLine(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("order.line.product_required", "Order line requires a product.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("order.line.quantity_invalid", "Order line quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("order.line.unit_price_invalid", "Order line unit price cannot be negative.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
