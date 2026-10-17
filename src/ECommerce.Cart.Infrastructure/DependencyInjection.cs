using ECommerce.Cart.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Cart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connection,
        string catalog,
        string order,
        string key)
    {
        services.AddDbContext<CartDbContext>(options => options.UseNpgsql(connection));

        services.AddScoped<ICartService, CartService>();

        services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
        {
            client.BaseAddress = new Uri(catalog.TrimEnd('/') + "/");

            client.Timeout = TimeSpan.FromSeconds(10);

        });

        services.AddHttpClient<IOrderClient, OrderClient>(client =>
        {
            client.BaseAddress = new Uri(order.TrimEnd('/') + "/");

            client.Timeout = TimeSpan.FromSeconds(30);

            client.DefaultRequestHeaders.Add("X-Service-Key", key);

        });

        return services;
    }

}
