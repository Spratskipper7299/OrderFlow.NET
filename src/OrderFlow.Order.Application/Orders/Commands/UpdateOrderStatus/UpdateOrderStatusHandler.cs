using MediatR;
using OrderFlow.Order.Application.Orders.Repositories;
using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand>
{
    private readonly IOrderCommandRepository _orders;

    public UpdateOrderStatusHandler(IOrderCommandRepository orders)
    {
        _orders = orders;
    }

    public async Task Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
            return;

        switch (request.NewStatus)
        {
            case OrderStatus.InventoryReserved:
                if (order.Status == OrderStatus.Submitted)
                    order.MarkInventoryReserved();
                break;
            case OrderStatus.PaymentAuthorized:
                if (order.Status == OrderStatus.InventoryReserved)
                    order.MarkPaymentAuthorized();
                break;
            case OrderStatus.Confirmed:
                if (order.Status == OrderStatus.PaymentAuthorized)
                    order.Confirm();
                break;
            case OrderStatus.Rejected:
                if (order.Status != OrderStatus.Rejected && order.Status != OrderStatus.Confirmed)
                    order.Reject(request.RejectionReason ?? "Rejected by Saga");
                break;
        }

        await _orders.SaveChangesAsync(cancellationToken);
    }
}
