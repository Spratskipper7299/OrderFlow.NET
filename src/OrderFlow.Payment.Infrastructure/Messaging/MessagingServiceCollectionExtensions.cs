using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Payment.Infrastructure.Data;
using System.Reflection;

namespace OrderFlow.Payment.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? consumersAssembly = null)
    {
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            if (consumersAssembly != null)
            {
                x.AddConsumers(consumersAssembly);
            }

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
