using System.Text.Json.Serialization;
using ECommerce.Cart.Api.Common;
using ECommerce.Cart.Infrastructure;
using ECommerce.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Cart.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var migrate = args.Contains("--migrate");

        var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());

        builder.AddLocalSecurityConfiguration();

        builder.Services.AddCommerceSecurity(builder.Configuration, migrate);

        builder.Services.AddControllers().AddJsonOptions(
            options => options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: false)));

        builder.Services.AddCommerceOpenApi();

        builder.Services.AddProblemDetails();

        builder.Services.AddExceptionHandler<ServiceExceptionHandler>();

        builder.Services.AddInfrastructure(
            builder.Configuration.GetConnectionString("CartDatabase") ?? throw new InvalidOperationException("CartDatabase connection is required."),
            builder.Configuration["Services:Catalog"] ?? "http://localhost:5171/",
            builder.Configuration["Services:Order"] ?? "http://localhost:5173/",
            migrate ? "migration" : SecurityExtensions.OutgoingKey(builder.Configuration, "Cart"));

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        var app = builder.Build();

        if (migrate)
        {
            using var scope = app.Services.CreateScope();

            await scope.ServiceProvider.GetRequiredService<CartDbContext>().Database.MigrateAsync();

            return;
        }

        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

            app.MapOpenApi();

            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Cart API"));

        }

        app.UseHttpsRedirection();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

        await app.RunAsync();

    }

}
