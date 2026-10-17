using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<InventoryService>();

        return services;
    }

}
