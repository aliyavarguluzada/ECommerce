using System.Text.Json.Serialization;
using ECommerce.Inventory.Api.Common;
using ECommerce.Inventory.Application;
using ECommerce.Inventory.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Security;
namespace ECommerce.Inventory.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var migrate = args.Contains("--migrate");
        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
        builder.AddLocalSecurityConfiguration();
        builder.Services.AddCommerceSecurity(builder.Configuration, migrate);
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(
            builder.Configuration.GetConnectionString("InventoryDatabase") ?? throw new InvalidOperationException("InventoryDatabase connection string is required."),
            builder.Configuration["Services:Catalog"] ?? "http://localhost:5171/");
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        var app = builder.Build();
        if (migrate)
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.Migrate();
            return;
        }
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Inventory API"));
        }
        app.UseHttpsRedirection();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.Run();
    }
}
