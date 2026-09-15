using MediatR;

namespace OrderFlow.Order.Application.Orders.Queries.GetOrder;

public sealed record GetOrderQuery(Guid Id) : IRequest<GetOrderResult?>;
