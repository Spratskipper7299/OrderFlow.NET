namespace OrderFlow.Contracts;

public record ReserveInventory(Guid OrderId, List<OrderItemDto> Items);

public record AuthorizePayment(Guid OrderId, decimal Amount, string PaymentToken);

public record ReleaseInventory(Guid OrderId, List<OrderItemDto> Items);

public record CommitInventory(Guid OrderId, List<OrderItemDto> Items);
