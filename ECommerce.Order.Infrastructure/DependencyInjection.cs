using ECommerce.Order.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ECommerce.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connection, string catalogUrl, string inventoryUrl, string paymentUrl, string serviceKey)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IOrderStore, OrderStore>();
        services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
        { client.BaseAddress = new Uri(catalogUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); });
        services.AddHttpClient<IInventoryClient, InventoryClient>(client =>
        { client.BaseAddress = new Uri(inventoryUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(10); client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey); });
        services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
        { client.BaseAddress = new Uri(paymentUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(30); client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey); });
        return services;
    }
}
