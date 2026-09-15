using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Sagas;

public record UpdateOrderInternalCommand(Guid OrderId, OrderStatus NewStatus, string? RejectionReason = null);
