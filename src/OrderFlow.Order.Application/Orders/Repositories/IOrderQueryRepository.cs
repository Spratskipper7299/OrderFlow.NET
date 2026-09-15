using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Repositories;

public interface IOrderQueryRepository
{
    Task<OrderFlow.Order.Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
