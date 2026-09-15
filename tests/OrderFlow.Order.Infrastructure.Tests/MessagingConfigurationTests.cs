using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Order.Application;
using OrderFlow.Order.Application.Orders.Commands.CreateOrder;
using OrderFlow.Order.Infrastructure;
using Xunit;

namespace OrderFlow.Order.Infrastructure.Tests;

public class MessagingConfigurationTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDb"] = "Server=.;Database=TestDb;Trusted_Connection=True;",
                ["ConnectionStrings:RabbitMq"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddOrderInfrastructure(configuration);

        // Disable hosted services so MassTransit doesn't try to connect to RabbitMQ
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = false;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false
        });
    }

    [Fact]
    public void IPublishEndpoint_CanBeResolved()
    {
        using var provider = BuildServiceProvider();

        var publishEndpoint = provider.GetService<IPublishEndpoint>();

        Assert.NotNull(publishEndpoint);
    }

    [Fact]
    public void IBus_CanBeResolved()
    {
        using var provider = BuildServiceProvider();

        var bus = provider.GetService<IBus>();

        Assert.NotNull(bus);
    }

    [Fact]
    public void CreateOrderHandler_CanBeResolved()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetService<ICommandHandler<CreateOrderCommand, CreateOrderResult>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void ISendEndpointProvider_CanBeResolved()
    {
        using var provider = BuildServiceProvider();

        var sendEndpointProvider = provider.GetService<ISendEndpointProvider>();

        Assert.NotNull(sendEndpointProvider);
    }
}
