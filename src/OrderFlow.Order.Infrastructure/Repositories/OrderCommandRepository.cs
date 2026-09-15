using Microsoft.EntityFrameworkCore;
using OrderFlow.Order.Application.Orders.Repositories;
using OrderFlow.Order.Infrastructure.Data;

namespace OrderFlow.Order.Infrastructure.Repositories;

public sealed class OrderCommandRepository : IOrderCommandRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderCommandRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddAsync(Domain.Orders.Order order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
