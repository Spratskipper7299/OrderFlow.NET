using OrderFlow.Payment.Domain.Payments;

namespace OrderFlow.Payment.Application.Repositories;

public interface IPaymentRepository
{
    Task<PaymentRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
