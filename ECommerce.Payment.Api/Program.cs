using System.Text.Json.Serialization;
using ECommerce.Payment.Api.Common;
using ECommerce.Payment.Infrastructure;
using ECommerce.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Payment.Api;
public class Program
{
    public static async Task Main(string[] args)
    {
        var migrate = args.Contains("--migrate");
        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
        builder.AddLocalSecurityConfiguration();

        builder.Services.AddCommerceSecurity(builder.Configuration, migrate);
        builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddOpenApi(); builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
        builder.Services.AddInfrastructure(
            builder.Configuration.GetConnectionString("PaymentDatabase") ?? throw new InvalidOperationException("PaymentDatabase connection is required."),
            builder.Configuration["Services:Order"] ?? "http://localhost:5173/", migrate ? "migration" : SecurityExtensions.OutgoingKey(builder.Configuration, "Payment"),
            builder.Environment.IsDevelopment() && builder.Configuration["Payments:Provider"] == "Development");
        builder.Services.AddHostedService<PaymentRecoveryWorker>();

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        var app = builder.Build();
        if (migrate)
        {
            using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.MigrateAsync();

            return;
        }

        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Payment API")); }
        app.UseHttpsRedirection(); app.UseAuthentication(); app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        await app.RunAsync();
    }
}
