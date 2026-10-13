using ECommerce.Cart.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace ECommerce.Cart.Api.Common;
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        if (!await db.Database.CanConnectAsync(ct)) return HealthCheckResult.Unhealthy("Database is unavailable.");
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) return HealthCheckResult.Unhealthy("Apply migrations.");
        return HealthCheckResult.Healthy();
    }
}
