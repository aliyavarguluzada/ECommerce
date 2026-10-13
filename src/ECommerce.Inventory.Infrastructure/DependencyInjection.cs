using ECommerce.Inventory.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ECommerce.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connection, string catalogUrl)
    {
        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IInventoryStore, InventoryStore>();
        services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
        { client.BaseAddress = new Uri(catalogUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); });
        return services;
    }
}
