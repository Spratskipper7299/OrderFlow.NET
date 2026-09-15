using MediatR;

namespace OrderFlow.Payment.Application.Commands;

public record AuthorizePaymentCommand(Guid OrderId, decimal Amount, string PaymentToken) : IRequest;
