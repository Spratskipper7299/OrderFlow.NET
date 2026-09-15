using OrderFlow.Payment.Domain.Exceptions;

namespace OrderFlow.Payment.Domain.Payments;

public class PaymentRecord
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    private PaymentRecord() { } // For EF Core

    private PaymentRecord(Guid id, Guid orderId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Payment amount must be greater than zero.");

        Id = id;
        OrderId = orderId;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }

    public static PaymentRecord Create(Guid id, Guid orderId, decimal amount)
    {
        return new PaymentRecord(id, orderId, amount);
    }

    public void Authorize()
    {
        if (Status == PaymentStatus.Authorized)
            throw new DomainException("Payment is already authorized.");
            
        if (Status == PaymentStatus.Rejected)
            throw new DomainException("Cannot authorize a rejected payment.");

        Status = PaymentStatus.Authorized;
    }

    public void Reject()
    {
        if (Status == PaymentStatus.Authorized)
            throw new DomainException("Cannot reject an already authorized payment.");
            
        if (Status == PaymentStatus.Rejected)
            throw new DomainException("Payment is already rejected.");

        Status = PaymentStatus.Rejected;
    }
}
