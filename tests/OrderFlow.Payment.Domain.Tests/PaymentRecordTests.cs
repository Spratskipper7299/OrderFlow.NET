using OrderFlow.Payment.Domain.Exceptions;
using OrderFlow.Payment.Domain.Payments;
using Xunit;

namespace OrderFlow.Payment.Domain.Tests;

public class PaymentRecordTests
{
    [Fact]
    public void Create_ValidInput_CreatesPayment()
    {
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var amount = 100.50m;

        var payment = PaymentRecord.Create(id, orderId, amount);

        Assert.Equal(id, payment.Id);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal(amount, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10.5)]
    public void Create_InvalidAmount_ThrowsDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), amount));
    }

    [Fact]
    public void Authorize_PendingPayment_SetsStatusToAuthorized()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);

        payment.Authorize();

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public void Authorize_AlreadyAuthorized_ThrowsDomainException()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);
        payment.Authorize();

        Assert.Throws<DomainException>(() => payment.Authorize());
    }

    [Fact]
    public void Authorize_RejectedPayment_ThrowsDomainException()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);
        payment.Reject();

        Assert.Throws<DomainException>(() => payment.Authorize());
    }

    [Fact]
    public void Reject_PendingPayment_SetsStatusToRejected()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);

        payment.Reject();

        Assert.Equal(PaymentStatus.Rejected, payment.Status);
    }

    [Fact]
    public void Reject_AlreadyRejected_ThrowsDomainException()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);
        payment.Reject();

        Assert.Throws<DomainException>(() => payment.Reject());
    }

    [Fact]
    public void Reject_AuthorizedPayment_ThrowsDomainException()
    {
        var payment = PaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 100m);
        payment.Authorize();

        Assert.Throws<DomainException>(() => payment.Reject());
    }
}
