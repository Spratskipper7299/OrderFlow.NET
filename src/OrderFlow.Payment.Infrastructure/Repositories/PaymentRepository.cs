using Microsoft.EntityFrameworkCore;
using OrderFlow.Payment.Application.Repositories;
using OrderFlow.Payment.Domain.Payments;
using OrderFlow.Payment.Infrastructure.Data;

namespace OrderFlow.Payment.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments.FindAsync([id], cancellationToken);
    }

    public async Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
    }

    public async Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        _dbContext.Payments.Update(payment);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
