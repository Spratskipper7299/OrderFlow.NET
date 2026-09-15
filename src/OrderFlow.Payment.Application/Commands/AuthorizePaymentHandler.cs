using MediatR;
using OrderFlow.Payment.Application.Repositories;
using OrderFlow.Payment.Domain.Payments;

namespace OrderFlow.Payment.Application.Commands;

public class AuthorizePaymentHandler : IRequestHandler<AuthorizePaymentCommand>
{
    private readonly IPaymentRepository _repository;

    public AuthorizePaymentHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(AuthorizePaymentCommand request, CancellationToken cancellationToken)
    {
        bool isNew = false;
        var payment = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        
        if (payment == null)
        {
            payment = PaymentRecord.Create(Guid.NewGuid(), request.OrderId, request.Amount);
            await _repository.AddAsync(payment, cancellationToken);
            isNew = true;
        }

        payment.Authorize();
        
        if (!isNew)
        {
            await _repository.UpdateAsync(payment, cancellationToken);
        }
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
