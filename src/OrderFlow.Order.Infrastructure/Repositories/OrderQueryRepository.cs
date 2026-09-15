using Microsoft.EntityFrameworkCore;
using OrderFlow.Order.Application.Orders.Repositories;
using OrderFlow.Order.Infrastructure.Data;

namespace OrderFlow.Order.Infrastructure.Repositories;

public sealed class OrderQueryRepository : IOrderQueryRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderQueryRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }
}
