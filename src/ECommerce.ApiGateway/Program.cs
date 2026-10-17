using ECommerce.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ECommerce.ApiGateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddLocalSecurityConfiguration();

        builder.Services.AddCommerceSecurity(builder.Configuration);

        builder.Services.AddProblemDetails();

        builder.Services.AddCommerceOpenApi();

        builder.Services.AddHttpClient("documentation", client => client.Timeout = TimeSpan.FromSeconds(10));

        builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy(
                "gateway",
                context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0 }));

        });

        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

        builder.Services.AddCors(options => options.AddPolicy("browser", policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();

            }

        }));

        var app = builder.Build();

        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            app.UseSwaggerUI(options =>
            {
                foreach (var service in DocumentationEndpoints.Services)
                {
                    options.SwaggerEndpoint($"/docs/{service}/openapi.json", service + " API");

                }

            });

            app.MapServiceDocumentation();

        }

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/shop"))
            {
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

                context.Response.Headers.XContentTypeOptions = "nosniff";

                context.Response.Headers.CacheControl = "no-cache";

            }

            context.Request.Headers.Remove("X-Service-Key");

            context.Request.Headers.Remove("X-User-Id");

            var path = Uri.UnescapeDataString(context.Request.Path.Value ?? "");

            if (path.Split('/').Any(segment => segment.Equals("internal", StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = 404;

                return;
            }

            await next(context);

        });

        app.UseHttpsRedirection();

        app.UseCors("browser");

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseRateLimiter();

        app.UseStaticFiles();

        app.MapGet("/", () => Results.Redirect("/shop/")).ExcludeFromDescription();

        app.MapGet("/shop/", () => Results.Redirect("/shop/index.html")).ExcludeFromDescription();

        app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

        app.MapReverseProxy().RequireRateLimiting("gateway");

        app.Run();

    }

}
