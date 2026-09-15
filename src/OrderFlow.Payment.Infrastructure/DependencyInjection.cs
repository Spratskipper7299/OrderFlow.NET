using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Payment.Application.Repositories;
using OrderFlow.Payment.Infrastructure.Data;
using OrderFlow.Payment.Infrastructure.Messaging;
using OrderFlow.Payment.Infrastructure.Repositories;
using System.Reflection;

namespace OrderFlow.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? consumersAssembly = null)
    {
        var connectionString = configuration.GetConnectionString("PaymentDb")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<PaymentDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddPaymentMessaging(configuration, consumersAssembly);

        return services;
    }
}
