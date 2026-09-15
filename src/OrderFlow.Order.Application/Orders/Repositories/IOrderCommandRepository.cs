using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Orders.Repositories;

public interface IOrderCommandRepository
{
    Task<OrderFlow.Order.Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(OrderFlow.Order.Domain.Orders.Order order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
