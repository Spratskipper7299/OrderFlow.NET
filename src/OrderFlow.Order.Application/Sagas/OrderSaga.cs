using System.Text.Json;
using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Order.Domain.Orders;

namespace OrderFlow.Order.Application.Sagas;

public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    public State PendingInventory { get; private set; } = null!;
    public State PendingPayment { get; private set; } = null!;
    public State PendingCommit { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Compensating { get; private set; } = null!;
    public State Rejected { get; private set; } = null!;

    public Event<OrderCreated> OrderCreatedEvent { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReservedEvent { get; private set; } = null!;
    public Event<InventoryRejected> InventoryRejectedEvent { get; private set; } = null!;
    public Event<PaymentAuthorized> PaymentAuthorizedEvent { get; private set; } = null!;
    public Event<PaymentRejected> PaymentRejectedEvent { get; private set; } = null!;
    public Event<InventoryCommitted> InventoryCommittedEvent { get; private set; } = null!;
    public Event<InventoryReleased> InventoryReleasedEvent { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreatedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryRejectedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentAuthorizedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentRejectedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryCommittedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReleasedEvent, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderCreatedEvent)
                .Then(context =>
                {
                    context.Saga.CorrelationId = context.Message.OrderId;
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.ItemsJson = JsonSerializer.Serialize(context.Message.Items);
                })
                .Publish(context => new ReserveInventory(
                    context.Message.OrderId,
                    context.Message.Items
                ))
                .TransitionTo(PendingInventory)
        );

        During(PendingInventory,
            When(InventoryReservedEvent)
                .Publish(context => new UpdateOrderInternalCommand(
                    context.Message.OrderId,
                    OrderStatus.InventoryReserved,
                    null
                ))
                .Publish(context => new AuthorizePayment(
                    context.Message.OrderId,
                    context.Saga.TotalAmount,
                    "dummy_token" // PaymentToken not needed realistically here, but required by contract
                ))
                .TransitionTo(PendingPayment),

            When(InventoryRejectedEvent)
                .Publish(context => new UpdateOrderInternalCommand(
                    context.Message.OrderId,
                    OrderStatus.Rejected,
                    context.Message.Reason
                ))
                .TransitionTo(Rejected)
        );

        During(PendingPayment,
            When(PaymentAuthorizedEvent)
                .Publish(context => new UpdateOrderInternalCommand(
                    context.Message.OrderId,
                    OrderStatus.PaymentAuthorized,
                    null
                ))
                .Publish(context => new CommitInventory(
                    context.Message.OrderId,
                    JsonSerializer.Deserialize<List<OrderItemDto>>(context.Saga.ItemsJson) ?? new List<OrderItemDto>()
                ))
                .TransitionTo(PendingCommit),

            When(PaymentRejectedEvent)
                .Publish(context => new ReleaseInventory(
                    context.Message.OrderId,
                    JsonSerializer.Deserialize<List<OrderItemDto>>(context.Saga.ItemsJson) ?? new List<OrderItemDto>()
                ))
                .TransitionTo(Compensating)
        );

        During(PendingCommit,
            When(InventoryCommittedEvent)
                .Publish(context => new UpdateOrderInternalCommand(
                    context.Message.OrderId,
                    OrderStatus.Confirmed,
                    null
                ))
                .TransitionTo(Confirmed)
        );

        During(Compensating,
            When(InventoryReleasedEvent)
                .Publish(context => new UpdateOrderInternalCommand(
                    context.Message.OrderId,
                    OrderStatus.Rejected,
                    "Payment failed and inventory was released."
                ))
                .TransitionTo(Rejected)
        );
    }
}
