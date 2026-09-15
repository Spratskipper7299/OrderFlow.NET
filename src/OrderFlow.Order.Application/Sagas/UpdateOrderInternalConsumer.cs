using MassTransit;
using MediatR;
using OrderFlow.Order.Application.Orders.Commands.UpdateOrderStatus;

namespace OrderFlow.Order.Application.Sagas;

public class UpdateOrderInternalConsumer : IConsumer<UpdateOrderInternalCommand>
{
    private readonly IMediator _mediator;

    public UpdateOrderInternalConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<UpdateOrderInternalCommand> context)
    {
        await _mediator.Send(new UpdateOrderStatusCommand(
            context.Message.OrderId,
            context.Message.NewStatus,
            context.Message.RejectionReason), context.CancellationToken);
    }
}
