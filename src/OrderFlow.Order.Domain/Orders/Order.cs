namespace OrderFlow.Order.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    private Order(Guid customerId, List<OrderLine> lines)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Submitted;
        CreatedAt = DateTimeOffset.UtcNow;
        _lines.AddRange(lines);
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// SQL Server <c>rowversion</c> mapped by EF Core. Unused in domain logic.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<OrderLine> Lines => _lines;

    public decimal Total => _lines.Sum(line => line.LineTotal);

    public static Order Create(Guid customerId, IReadOnlyList<OrderLineRequest> lines)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("order.customer_required", "Order requires a customer.");
        }

        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new DomainException("order.lines_required", "Order must have at least one line.");
        }

        var productIds = new HashSet<Guid>();
        var orderLines = new List<OrderLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!productIds.Add(line.ProductId))
            {
                throw new DomainException(
                    "order.duplicate_product",
                    "Order cannot contain more than one line for the same product.");
            }

            orderLines.Add(new OrderLine(line.ProductId, line.Quantity, line.UnitPrice));
        }

        return new Order(customerId, orderLines);
    }

    public void MarkInventoryReserved()
    {
        if (Status != OrderStatus.Submitted)
        {
            throw new DomainException(
                "order.invalid_status_transition",
                $"Order in status '{Status}' cannot transition to InventoryReserved.");
        }
        Status = OrderStatus.InventoryReserved;
    }

    public void MarkPaymentAuthorized()
    {
        if (Status != OrderStatus.InventoryReserved)
        {
            throw new DomainException(
                "order.invalid_status_transition",
                $"Order in status '{Status}' cannot transition to PaymentAuthorized.");
        }
        Status = OrderStatus.PaymentAuthorized;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.PaymentAuthorized)
        {
            throw new DomainException(
                "order.invalid_status_transition",
                $"Order in status '{Status}' cannot be confirmed.");
        }
        Status = OrderStatus.Confirmed;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        
        if (Status is not (OrderStatus.Submitted or OrderStatus.InventoryReserved or OrderStatus.PaymentAuthorized))
        {
            throw new DomainException(
                "order.invalid_status_transition",
                $"Order in status '{Status}' cannot be rejected.");
        }
        
        Status = OrderStatus.Rejected;
        RejectionReason = reason.Trim();
    }
}
