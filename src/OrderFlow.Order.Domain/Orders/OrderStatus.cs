namespace OrderFlow.Order.Domain.Orders;

public enum OrderStatus
{
    Submitted = 0,
    InventoryReserved = 1,
    PaymentAuthorized = 2,
    Confirmed = 3,
    Rejected = 4
}
