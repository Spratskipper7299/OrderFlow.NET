using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Order.Application.Common.Behaviors;
using OrderFlow.Order.Application.Orders.Commands.CreateOrder;
using OrderFlow.Order.Application.Orders.Queries.GetOrder;

namespace OrderFlow.Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateOrderCommand, CreateOrderResult>, CreateOrderHandler>();
        services.AddScoped<IQueryHandler<GetOrderQuery, GetOrderResult?>, GetOrderHandler>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        return services;
    }
}
