using MediatR;
using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Commands.UpdateOrderStatus;

public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus, string? RejectionReason = null) : IRequest;
