using MassTransit;
using MediatR;
using OrderFlow.Contracts;
using OrderFlow.Payment.Application.Commands;
using OrderFlow.Payment.Application.Repositories;
using OrderFlow.Payment.Domain.Exceptions;

namespace OrderFlow.Payment.Worker.Consumers;

public class AuthorizePaymentConsumer : IConsumer<AuthorizePayment>
{
    private readonly IMediator _mediator;
    private readonly IPaymentRepository _repository;

    public AuthorizePaymentConsumer(IMediator mediator, IPaymentRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<AuthorizePayment> context)
    {
        var command = context.Message;

        try
        {
            await _mediator.Send(new AuthorizePaymentCommand(command.OrderId, command.Amount, command.PaymentToken), context.CancellationToken);

            await context.Publish(new PaymentAuthorized(command.OrderId, command.PaymentToken), context.CancellationToken);
            await _repository.SaveChangesAsync(context.CancellationToken);
        }
        catch (DomainException ex)
        {
            await context.Publish(new PaymentRejected(command.OrderId, ex.Message), context.CancellationToken);
            await _repository.SaveChangesAsync(context.CancellationToken);
        }
    }
}
