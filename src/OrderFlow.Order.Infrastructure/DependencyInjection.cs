using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Order.Application.Orders.Repositories;
using OrderFlow.Order.Infrastructure.Data;
using OrderFlow.Order.Infrastructure.Messaging;
using OrderFlow.Order.Infrastructure.Repositories;

namespace OrderFlow.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("OrderDb")
                ?? config.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IOrderCommandRepository, OrderCommandRepository>();
        services.AddScoped<IOrderQueryRepository, OrderQueryRepository>();

        services.AddOrderMessaging(configuration);

        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost";
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
            StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
            
        services.AddScoped<OrderFlow.Order.Application.Idempotency.IIdempotencyService, OrderFlow.Order.Infrastructure.Redis.RedisIdempotencyService>();

        return services;
    }
}
