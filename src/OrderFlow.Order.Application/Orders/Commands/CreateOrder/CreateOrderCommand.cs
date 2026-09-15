using MediatR;

namespace OrderFlow.Order.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderLine(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<CreateOrderLine> Lines) : IRequest<CreateOrderResult>;
