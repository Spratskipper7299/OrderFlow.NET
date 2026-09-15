using MediatR;
using OrderFlow.Order.Application.Orders.Repositories;

namespace OrderFlow.Order.Application.Orders.Queries.GetOrder;

public sealed class GetOrderHandler : IQueryHandler<GetOrderQuery, GetOrderResult?>, IRequestHandler<GetOrderQuery, GetOrderResult?>
{
    private readonly IOrderQueryRepository _orders;

    public GetOrderHandler(IOrderQueryRepository orders)
    {
        _orders = orders;
    }

    public Task<GetOrderResult?> Handle(
        GetOrderQuery request,
        CancellationToken cancellationToken)
    {
        return HandleAsync(request, cancellationToken);
    }

    public async Task<GetOrderResult?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await _orders.GetByIdAsync(query.Id, cancellationToken);
        if (order == null)
        {
            return null;
        }

        var lines = order.Lines
            .Select(l => new GetOrderLineResult(l.ProductId, l.Quantity, l.UnitPrice))
            .ToList();

        return new GetOrderResult(
            order.Id,
            order.CustomerId,
            order.Status,
            order.Total,
            order.CreatedAt,
            order.RejectionReason,
            lines);
    }
}
