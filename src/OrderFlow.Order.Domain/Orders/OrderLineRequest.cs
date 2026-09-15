namespace OrderFlow.Order.Domain.Orders;

public readonly record struct OrderLineRequest(Guid ProductId, int Quantity, decimal UnitPrice);
