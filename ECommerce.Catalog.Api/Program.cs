using ECommerce.Catalog.Api.Common;
using ECommerce.Catalog.Application;
using ECommerce.Catalog.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Catalog.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var migrate = args.Contains("--migrate");
        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<CatalogExceptionHandler>();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(
            builder.Configuration.GetConnectionString("CatalogDatabase")
            ?? throw new InvalidOperationException("CatalogDatabase connection string is required."));
        builder.Services.AddHealthChecks().AddCheck<CatalogDatabaseHealthCheck>("database", tags: ["ready"]);

        var app = builder.Build();
        if (migrate)
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.Migrate();
            return;
        }
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Catalog API"));
        }
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.Run();
    }
}
