namespace OrderFlow.Contracts;

public record OrderCreated(Guid OrderId, Guid CustomerId, decimal TotalAmount, List<OrderItemDto> Items);

public record InventoryReserved(Guid OrderId);

public record InventoryRejected(Guid OrderId, string Reason);

public record PaymentAuthorized(Guid OrderId, string TransactionId);

public record PaymentRejected(Guid OrderId, string Reason);

public record InventoryCommitted(Guid OrderId);

public record InventoryReleased(Guid OrderId);

public record OrderConfirmed(Guid OrderId);

public record OrderRejected(Guid OrderId, string Reason);
