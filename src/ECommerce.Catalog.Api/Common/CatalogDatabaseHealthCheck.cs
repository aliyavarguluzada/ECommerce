using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Api.Common;

public sealed class CatalogDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Catalog database is unavailable.");
        }

        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            return HealthCheckResult.Unhealthy("Catalog database migrations must be applied.");
        }

        return HealthCheckResult.Healthy();
    }

}
