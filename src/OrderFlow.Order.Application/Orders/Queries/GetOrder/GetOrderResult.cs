using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Queries.GetOrder;

public sealed record GetOrderLineResult(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record GetOrderResult(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    string? RejectionReason,
    IReadOnlyList<GetOrderLineResult> Lines);
