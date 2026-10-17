using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<OrderService>();

        return services;
    }

}
