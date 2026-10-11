using ECommerce.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
namespace ECommerce.ApiGateway;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddLocalSecurityConfiguration(); builder.Services.AddCommerceSecurity(builder.Configuration);
        builder.Services.AddProblemDetails();
        builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("gateway", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy("browser", policy =>
        { if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader(); }));
        var app = builder.Build(); app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            context.Request.Headers.Remove("X-Service-Key");
            context.Request.Headers.Remove("X-User-Id");
            var path = Uri.UnescapeDataString(context.Request.Path.Value ?? "");
            if (path.Split('/').Any(segment => segment.Equals("internal", StringComparison.OrdinalIgnoreCase)))
            { context.Response.StatusCode = 404; return; }
            await next(context);
        });
        app.UseHttpsRedirection(); app.UseCors("browser"); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
        app.MapReverseProxy().RequireRateLimiting("gateway");
        app.Run();
    }
}
