using ECommerce.Payment.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connection,
        string order,
        string key,
        bool developmentProvider)
    {
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connection));

        services.AddScoped<IPaymentService, PaymentService>();

        services.AddSingleton<IPaymentProvider>(new DevelopmentPaymentProvider(developmentProvider));

        services.AddHttpClient<IOrderClient, OrderClient>(client =>
        {
            client.BaseAddress = new Uri(order.TrimEnd('/') + "/");

            client.Timeout = TimeSpan.FromSeconds(30);

            client.DefaultRequestHeaders.Add("X-Service-Key", key);

        });

        return services;
    }

}
