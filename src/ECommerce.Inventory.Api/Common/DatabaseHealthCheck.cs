using ECommerce.Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Inventory.Api.Common;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        if (!await db.Database.CanConnectAsync(ct))
        {
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }

        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            return HealthCheckResult.Unhealthy("Database migrations must be applied.");
        }

        return HealthCheckResult.Healthy();
    }

}
