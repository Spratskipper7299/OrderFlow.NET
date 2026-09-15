using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFlow.Contracts;
using OrderFlow.Order.Application;
using OrderFlow.Order.Domain.Orders;
using OrderFlow.Order.Infrastructure;
using OrderFlow.Order.Infrastructure.Data;
using Testcontainers.MsSql;
using Xunit;
using MassTransit.Testing;
using OrderFlow.Order.Application.Sagas;

namespace OrderFlow.Order.Infrastructure.Tests;

public class OrderSagaIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private IHost? _host;
    private IServiceProvider _services = default!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDb"] = _sqlContainer.GetConnectionString(),
                // Use a dummy connection string to prevent actual RabbitMq from breaking
                ["ConnectionStrings:RabbitMq"] = "amqp://guest:guest@localhost:5672/"
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddApplication();
        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderDb")));
        builder.Services.AddScoped<OrderFlow.Order.Application.Orders.Repositories.IOrderCommandRepository, OrderFlow.Order.Infrastructure.Repositories.OrderCommandRepository>();
        builder.Services.AddScoped<OrderFlow.Order.Application.Orders.Repositories.IOrderQueryRepository, OrderFlow.Order.Infrastructure.Repositories.OrderQueryRepository>();

        // Register MassTransit TestHarness
        builder.Services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<UpdateOrderInternalConsumer>();

            x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<OrderDbContext>();
                    r.UseSqlServer();
                });

            // We do not need the outbox for this test harness, it simplifies things.
        });

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await _host.StartAsync();

        _services = _host.Services;
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        await _sqlContainer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task HappyPath_OrderCreated_EventuallyReachesConfirmedStatus()
    {
        // Arrange
        using var scope = _services.CreateScope();
        var harness = _host!.Services.GetRequiredService<ITestHarness>();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = Domain.Orders.Order.Create(customerId, [new OrderLineRequest(productId, 1, 100m)]);
        
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var orderId = order.Id;
        var items = new List<OrderItemDto> { new(productId, 1) };

        // Act 1: Publish OrderCreated
        await harness.Bus.Publish(new OrderCreated(orderId, customerId, 100m, items));
        Assert.True(await harness.Consumed.Any<OrderCreated>());

        // Wait for Saga to process OrderCreated and reach PendingInventory
        await WaitForSagaState(orderId, "PendingInventory");

        // Act 2: Simulate Inventory Worker publishing InventoryReserved
        await harness.Bus.Publish(new InventoryReserved(orderId));
        Assert.True(await harness.Consumed.Any<InventoryReserved>());

        // Wait for Saga to process InventoryReserved and reach PendingPayment
        await WaitForSagaState(orderId, "PendingPayment");

        // Wait for internal command to update Order aggregate to InventoryReserved
        await WaitForOrderStatus(orderId, OrderStatus.InventoryReserved);

        // Act 3: Simulate Payment Worker publishing PaymentAuthorized
        await harness.Bus.Publish(new PaymentAuthorized(orderId, "txn_123"));
        Assert.True(await harness.Consumed.Any<PaymentAuthorized>());

        // Wait for Saga to process PaymentAuthorized and reach PendingCommit
        await WaitForSagaState(orderId, "PendingCommit");

        // Wait for internal command to update Order aggregate to PaymentAuthorized
        await WaitForOrderStatus(orderId, OrderStatus.PaymentAuthorized);

        // Verify Order is NOT Confirmed yet
        using (var validationScope = _services.CreateScope())
        {
            var db = validationScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var checkOrder = await db.Orders.FindAsync(orderId);
            Assert.NotEqual(OrderStatus.Confirmed, checkOrder!.Status);
        }

        // Act 4: Simulate Inventory Worker publishing InventoryCommitted
        await harness.Bus.Publish(new InventoryCommitted(orderId));
        Assert.True(await harness.Consumed.Any<InventoryCommitted>());

        // Wait for Saga to process InventoryCommitted and reach Confirmed
        await WaitForSagaState(orderId, "Confirmed");

        // Wait for internal command to update Order aggregate to Confirmed
        await WaitForOrderStatus(orderId, OrderStatus.Confirmed);
    }

    [Fact]
    public async Task CompensationPath_PaymentRejected_EventuallyReachesRejectedStatus()
    {
        var harness = _host!.Services.GetRequiredService<ITestHarness>();
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = Domain.Orders.Order.Create(customerId, [new OrderLineRequest(productId, 1, 100m)]);
        
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var orderId = order.Id;
        var items = new List<OrderItemDto> { new(productId, 1) };

        // Act 1: Publish OrderCreated and InventoryReserved
        await harness.Bus.Publish(new OrderCreated(orderId, customerId, 100m, items));
        Assert.True(await harness.Consumed.Any<OrderCreated>());
        await WaitForSagaState(orderId, "PendingInventory");

        await harness.Bus.Publish(new InventoryReserved(orderId));
        Assert.True(await harness.Consumed.Any<InventoryReserved>());
        await WaitForSagaState(orderId, "PendingPayment");
        await WaitForOrderStatus(orderId, OrderStatus.InventoryReserved);

        // Act 2: Simulate Payment Rejection
        await harness.Bus.Publish(new PaymentRejected(orderId, "Card declined"));
        Assert.True(await harness.Consumed.Any<PaymentRejected>());

        // Wait for Saga to reach Compensating
        await WaitForSagaState(orderId, "Compensating");

        // Act 3: Simulate Inventory releasing
        await harness.Bus.Publish(new InventoryReleased(orderId));
        Assert.True(await harness.Consumed.Any<InventoryReleased>());

        // Wait for Saga to reach Rejected
        await WaitForSagaState(orderId, "Rejected");

        // Wait for Order status to be Rejected
        await WaitForOrderStatus(orderId, OrderStatus.Rejected);
    }

    private async Task WaitForSagaState(Guid correlationId, string expectedState)
    {
        var pollDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < pollDeadline)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var saga = await context.SagaStates.FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
            
            if (saga != null && saga.CurrentState == expectedState)
                return;

            await Task.Delay(250);
        }

        Assert.Fail($"Saga state did not reach {expectedState} within timeout.");
    }

    private async Task WaitForOrderStatus(Guid orderId, OrderStatus expectedStatus)
    {
        var pollDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < pollDeadline)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            
            if (order != null && order.Status == expectedStatus)
                return;

            await Task.Delay(250);
        }

        Assert.Fail($"Order status did not reach {expectedStatus} within timeout.");
    }
}
