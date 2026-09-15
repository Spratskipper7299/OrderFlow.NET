using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderResult(Guid OrderId, OrderStatus Status, decimal Total);
