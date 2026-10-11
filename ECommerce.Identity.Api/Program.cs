using System.Text.Json.Serialization;
using ECommerce.Identity.Api.Common;
using ECommerce.Identity.Infrastructure;
using ECommerce.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
namespace ECommerce.Identity.Api;
public class Program
{
    public static async Task Main(string[] args)
    {
        var migrate = args.Contains("--migrate"); var bootstrap = args.Contains("--bootstrap-admin");
        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate" && x != "--bootstrap-admin").ToArray());
        builder.AddLocalSecurityConfiguration();
        builder.Configuration.AddJsonFile(SecurityExtensions.LocalConfigurationPath(builder.Environment.ContentRootPath, "identity.private.json"), optional: true).AddEnvironmentVariables();
        builder.Services.AddCommerceSecurity(builder.Configuration, migrate || bootstrap);
        builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddOpenApi(); builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
        builder.Services.AddInfrastructure(
            builder.Configuration.GetConnectionString("IdentityDatabase") ?? throw new InvalidOperationException("IdentityDatabase connection is required."));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("identity", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        var app = builder.Build();
        if (migrate)
        {
            using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
            await DependencyInjection.SeedRolesAsync(scope.ServiceProvider);
            return;
        }
        if (bootstrap)
        {
            using var scope = app.Services.CreateScope();
            await DependencyInjection.BootstrapAdminAsync(scope.ServiceProvider,
                builder.Configuration["BootstrapAdmin:Email"] ?? "", builder.Configuration["BootstrapAdmin:Password"] ?? "");
            return;
        }
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Identity API")); }
        app.UseHttpsRedirection(); app.UseAuthentication(); app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        await app.RunAsync();
    }
}
