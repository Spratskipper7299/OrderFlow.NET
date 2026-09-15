using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Order.Infrastructure.Data;

namespace OrderFlow.Order.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderFlow.Order.Application.Sagas.UpdateOrderInternalConsumer>();

            x.AddSagaStateMachine<OrderFlow.Order.Application.Sagas.OrderSaga, OrderFlow.Order.Application.Sagas.OrderSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<OrderDbContext>();
                    r.UseSqlServer();
                });

            x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMq");
                if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
                {
                    if (Uri.TryCreate(rabbitMqConnectionString, UriKind.Absolute, out var hostUri))
                    {
                        cfg.Host(hostUri);
                    }
                    else
                    {
                        cfg.Host(rabbitMqConnectionString);
                    }
                }
                else
                {
                    cfg.Host("localhost", "/", h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    });
                }

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
