using ECommerce.Catalog.Application.Abstractions;
using ECommerce.Catalog.Infrastructure.Persistence;
using ECommerce.Catalog.Infrastructure.Persistence.Repositories;
using ECommerce.Catalog.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<IProductRepository, ProductRepository>();

        services.AddScoped<IProductQueries, ProductQueries>();

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CatalogDbContext>());

        return services;
    }

}
