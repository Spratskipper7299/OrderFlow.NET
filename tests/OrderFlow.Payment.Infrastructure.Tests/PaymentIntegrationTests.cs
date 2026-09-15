using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFlow.Contracts;
using OrderFlow.Payment.Application;
using OrderFlow.Payment.Domain.Payments;
using OrderFlow.Payment.Infrastructure.Data;
using OrderFlow.Payment.Worker.Consumers;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace OrderFlow.Payment.Infrastructure.Tests;

public class PaymentIntegrationTests : IAsyncLifetime
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
                ["ConnectionStrings:PaymentDb"] = _sqlContainer.GetConnectionString(),
                ["ConnectionStrings:RabbitMq"] = _rabbitMqContainer.GetConnectionString()
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddPaymentApplication();
        builder.Services.AddPaymentInfrastructure(builder.Configuration, typeof(AuthorizePaymentConsumer).Assembly);

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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
        await Task.WhenAll(_sqlContainer.DisposeAsync().AsTask(), _rabbitMqContainer.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Consume_AuthorizePayment_SuccessfullyAuthorizesAndPersistsPayment()
    {
        // Arrange
        using var scope = _services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var orderId = Guid.NewGuid();
        var authorizeCommand = new AuthorizePayment(orderId, 150.75m, "token-integration-123");

        // Act: publish to RabbitMQ — consumer processes asynchronously
        await bus.Publish(authorizeCommand);

        // Assert: poll the database until the PaymentRecord appears (max 15 s)
        PaymentRecord? paymentRecord = null;
        var pollDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < pollDeadline)
        {
            using var checkScope = _services.CreateScope();
            var checkContext = checkScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            paymentRecord = await checkContext.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Authorized);
            if (paymentRecord != null)
                break;
            await Task.Delay(250);
        }

        Assert.NotNull(paymentRecord);
        Assert.Equal(orderId, paymentRecord.OrderId);
        Assert.Equal(150.75m, paymentRecord.Amount);
        Assert.Equal(PaymentStatus.Authorized, paymentRecord.Status);
    }
}
    



