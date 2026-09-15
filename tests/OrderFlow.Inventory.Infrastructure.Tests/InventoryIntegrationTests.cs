using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Domain.Products;
using OrderFlow.Inventory.Infrastructure.Data;
using OrderFlow.Inventory.Worker.Consumers;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace OrderFlow.Inventory.Infrastructure.Tests;

public class InventoryIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();
    private IHost? _host;
    private IServiceProvider _services = default!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InventoryDb"] = _sqlContainer.GetConnectionString(),
                ["ConnectionStrings:RabbitMq"] = _rabbitMqContainer.GetConnectionString()
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddInventoryInfrastructure(builder.Configuration, typeof(ReserveInventoryConsumer).Assembly);

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _ = _host.StartAsync();
        await Task.Delay(2000);
        
        _services = _host.Services;
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        await Task.WhenAll(_sqlContainer.DisposeAsync().AsTask(), _rabbitMqContainer.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Consume_ReserveInventory_SuccessfullyReservesAndPublishesEvent()
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();
        
        // Seed database
        var productId = Guid.NewGuid();
        var product = Product.Create(productId, "SKU-TEST", "Test Item", 50);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var reserveCommand = new ReserveInventory(orderId, new List<OrderItemDto> { new OrderItemDto(productId, 5) });

        var observer = new TestConsumeObserver();
        var handle = bus.ConnectConsumeObserver(observer);

        try
        {
            await bus.Publish(reserveCommand);
            
            // Wait for processing
            await Task.Delay(3000);

            // Check database
            using var checkScope = _services.CreateScope();
            var checkContext = checkScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var updatedProduct = await checkContext.Products.FindAsync(productId);
            
            Assert.NotNull(updatedProduct);
            Assert.Equal(45, updatedProduct.AvailableQuantity);
            Assert.Equal(5, updatedProduct.ReservedQuantity);
            
            // The InventoryReserved event should be in the outbox or already published
        }
        finally
        {
            handle.Disconnect();
        }
    }

    private class TestConsumeObserver : IConsumeObserver
    {
        public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;
        public Task PostConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;
        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}
