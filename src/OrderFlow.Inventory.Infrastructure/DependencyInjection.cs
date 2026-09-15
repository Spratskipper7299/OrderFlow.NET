using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Inventory.Application.Repositories;
using OrderFlow.Inventory.Infrastructure.Data;
using OrderFlow.Inventory.Infrastructure.Messaging;
using OrderFlow.Inventory.Infrastructure.Repositories;

namespace OrderFlow.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        System.Reflection.Assembly? consumersAssembly = null)
    {
        var connectionString = configuration.GetConnectionString("InventoryDb")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<InventoryDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddInventoryMessaging(configuration, consumersAssembly);

        return services;
    }
}
