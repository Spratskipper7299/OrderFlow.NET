using MassTransit;

namespace OrderFlow.Order.Application.Sagas;

public class OrderSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    
    // Additional data to carry across states (like CustomerId or Total)
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string ItemsJson { get; set; } = string.Empty;

    /// <summary>
    /// For SQL Server optimistic concurrency control (optional, but good practice for EF Core)
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
