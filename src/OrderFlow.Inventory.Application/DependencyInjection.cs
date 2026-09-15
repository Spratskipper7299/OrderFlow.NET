using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        return services;
    }
}
