using OrderFlow.Order.Domain.Orders;
using Xunit;

namespace OrderFlow.Order.Domain.Tests;

public class OrderTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly List<OrderLineRequest> _lines = new() { new(Guid.NewGuid(), 1, 10m) };

    [Fact]
    public void Create_ValidInputs_ShouldBeSubmitted()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);

        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void MarkInventoryReserved_WhenSubmitted_ShouldTransitionToInventoryReserved()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        
        order.MarkInventoryReserved();

        Assert.Equal(OrderStatus.InventoryReserved, order.Status);
    }

    [Fact]
    public void MarkInventoryReserved_WhenAlreadyReserved_ShouldThrowDomainException()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order.MarkInventoryReserved();

        var exception = Assert.Throws<DomainException>(() => order.MarkInventoryReserved());
        Assert.Equal("order.invalid_status_transition", exception.Code);
    }

    [Fact]
    public void MarkPaymentAuthorized_WhenInventoryReserved_ShouldTransitionToPaymentAuthorized()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order.MarkInventoryReserved();
        
        order.MarkPaymentAuthorized();

        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);
    }

    [Fact]
    public void MarkPaymentAuthorized_WhenSubmitted_ShouldThrowDomainException()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);

        var exception = Assert.Throws<DomainException>(() => order.MarkPaymentAuthorized());
        Assert.Equal("order.invalid_status_transition", exception.Code);
    }

    [Fact]
    public void Confirm_WhenPaymentAuthorized_ShouldTransitionToConfirmed()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order.MarkInventoryReserved();
        order.MarkPaymentAuthorized();
        
        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_WhenInventoryReserved_ShouldThrowDomainException()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order.MarkInventoryReserved();

        var exception = Assert.Throws<DomainException>(() => order.Confirm());
        Assert.Equal("order.invalid_status_transition", exception.Code);
    }

    [Theory]
    [InlineData("Insufficient inventory")]
    [InlineData("Payment declined")]
    public void Reject_ValidStates_ShouldTransitionToRejected(string reason)
    {
        var order1 = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order1.Reject(reason);
        Assert.Equal(OrderStatus.Rejected, order1.Status);
        Assert.Equal(reason, order1.RejectionReason);

        var order2 = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order2.MarkInventoryReserved();
        order2.Reject(reason);
        Assert.Equal(OrderStatus.Rejected, order2.Status);

        var order3 = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order3.MarkInventoryReserved();
        order3.MarkPaymentAuthorized();
        order3.Reject(reason);
        Assert.Equal(OrderStatus.Rejected, order3.Status);
    }

    [Fact]
    public void Reject_WhenConfirmed_ShouldThrowDomainException()
    {
        var order = Order.Domain.Orders.Order.Create(_customerId, _lines);
        order.MarkInventoryReserved();
        order.MarkPaymentAuthorized();
        order.Confirm();

        var exception = Assert.Throws<DomainException>(() => order.Reject("Late rejection"));
        Assert.Equal("order.invalid_status_transition", exception.Code);
    }
}
