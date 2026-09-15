using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using OrderFlow.Inventory.Infrastructure.Data;
using OrderFlow.Payment.Infrastructure.Data;
using OrderFlow.Order.Infrastructure.Data;

namespace OrderFlow.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine").Build();

    public WebApplicationFactory<Program> ApiFactory { get; private set; } = default!;
    public IHost? InventoryWorkerHost { get; private set; }
    public IHost? PaymentWorkerHost { get; private set; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sqlContainer.StartAsync(),
            _rabbitMqContainer.StartAsync(),
            _redisContainer.StartAsync());

        var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sqlContainer.GetConnectionString());
        sqlBuilder.InitialCatalog = "OrderDb";
        var orderSqlStr = sqlBuilder.ConnectionString;
        
        sqlBuilder.InitialCatalog = "InventoryDb";
        var inventorySqlStr = sqlBuilder.ConnectionString;
        
        sqlBuilder.InitialCatalog = "PaymentDb";
        var paymentSqlStr = sqlBuilder.ConnectionString;

        Environment.SetEnvironmentVariable("ConnectionStrings:OrderDb", orderSqlStr);
        Environment.SetEnvironmentVariable("ConnectionStrings:InventoryDb", inventorySqlStr);
        Environment.SetEnvironmentVariable("ConnectionStrings:PaymentDb", paymentSqlStr);
        Environment.SetEnvironmentVariable("ConnectionStrings:RabbitMq", _rabbitMqContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings:Redis", _redisContainer.GetConnectionString());

        // 1. Start Order API
        ApiFactory = new WebApplicationFactory<Program>();
        
        // Ensure API starts and migrates DB
        _ = ApiFactory.Services;

        // 2. Start Inventory Worker
        var inventoryBuilder = Host.CreateApplicationBuilder();
        OrderFlow.Inventory.Application.DependencyInjection.AddInventoryApplication(inventoryBuilder.Services);
        OrderFlow.Inventory.Infrastructure.DependencyInjection.AddInventoryInfrastructure(inventoryBuilder.Services, inventoryBuilder.Configuration, typeof(OrderFlow.Inventory.Worker.Consumers.ReserveInventoryConsumer).Assembly);
        InventoryWorkerHost = inventoryBuilder.Build();
        
        using (var scope = InventoryWorkerHost.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        await InventoryWorkerHost.StartAsync();

        // 3. Start Payment Worker
        var paymentBuilder = Host.CreateApplicationBuilder();
        OrderFlow.Payment.Application.DependencyInjection.AddPaymentApplication(paymentBuilder.Services);
        OrderFlow.Payment.Infrastructure.DependencyInjection.AddPaymentInfrastructure(paymentBuilder.Services, paymentBuilder.Configuration, typeof(OrderFlow.Payment.Worker.Consumers.AuthorizePaymentConsumer).Assembly);
        PaymentWorkerHost = paymentBuilder.Build();
        
        using (var scope = PaymentWorkerHost.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        await PaymentWorkerHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (InventoryWorkerHost != null)
        {
            await InventoryWorkerHost.StopAsync();
            InventoryWorkerHost.Dispose();
        }
        
        if (PaymentWorkerHost != null)
        {
            await PaymentWorkerHost.StopAsync();
            PaymentWorkerHost.Dispose();
        }

        if (ApiFactory != null)
        {
            await ApiFactory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("ConnectionStrings:OrderDb", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:InventoryDb", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:PaymentDb", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:RabbitMq", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:Redis", null);

        await Task.WhenAll(
            _sqlContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask());
    }
}

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>;
