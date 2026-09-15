using MassTransit;
using MediatR;
using OrderFlow.Contracts;
using OrderFlow.Order.Application.Orders.Repositories;
using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>, IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderCommandRepository _orders;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateOrderHandler(IOrderCommandRepository orders, IPublishEndpoint publishEndpoint)
    {
        _orders = orders;
        _publishEndpoint = publishEndpoint;
    }

    public Task<CreateOrderResult> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        return HandleAsync(request, cancellationToken);
    }

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lineRequests = (command.Lines ?? [])
            .Select(line => new OrderLineRequest(line.ProductId, line.Quantity, line.UnitPrice))
            .ToList();

        var order = Order.Domain.Orders.Order.Create(command.CustomerId, lineRequests);

        await _orders.AddAsync(order, cancellationToken);
        
        var items = (command.Lines ?? []).Select(l => new OrderItemDto(l.ProductId, l.Quantity)).ToList();
        await _publishEndpoint.Publish(new OrderCreated(order.Id, order.CustomerId, order.Total, items), cancellationToken);
        
        await _orders.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id, order.Status, order.Total);
    }
}
