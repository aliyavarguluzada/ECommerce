using ECommerce.Catalog.Application;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ICatalogService, CatalogService>();

        return services;
    }

}
