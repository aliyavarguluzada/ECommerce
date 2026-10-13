using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace ECommerce.Security;

public static class SecurityExtensions
{
    public const string ServiceScheme = "ServiceKey";
    public static void AddLocalSecurityConfiguration(this WebApplicationBuilder builder)
    {
        var path = LocalConfigurationPath(builder.Environment.ContentRootPath, "security.local.json");
        builder.Configuration.AddJsonFile(path, optional: true).AddEnvironmentVariables();
    }
    public static string LocalConfigurationPath(string contentRoot, string filename)
    {
        var local = Path.Combine(contentRoot, filename);
        if (File.Exists(local)) return local;
        var parent = Path.GetFullPath(Path.Combine(contentRoot, "..", filename));
        return File.Exists(parent) ? parent : Path.GetFullPath(Path.Combine(contentRoot, "..", "..", filename));
    }
    public static IServiceCollection AddCommerceSecurity(this IServiceCollection services, IConfiguration config, bool migration = false)
    {
        RSA? rsa = null;
        if (!migration)
        {
            var key = config["Auth:PublicKey"] ?? throw new InvalidOperationException("Auth:PublicKey must contain a base64 RSA public key. Run scripts/Initialize-Development.ps1 first.");
            rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
        }
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = config["Auth:Issuer"] ?? "ecommerce-identity",
                ValidateAudience = true, ValidAudience = config["Auth:Audience"] ?? "ecommerce",
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidateIssuerSigningKey = true, IssuerSigningKey = rsa is null ? null : new RsaSecurityKey(rsa),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                NameClaimType = "sub", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id) || id == Guid.Empty)
                        context.Fail("Invalid user identity.");
                    return Task.CompletedTask;
                }
            };
        }).AddScheme<AuthenticationSchemeOptions, ServiceKeyHandler>(ServiceScheme, _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Customer", policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Customer"));
            options.AddPolicy("Admin", policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Admin"));
            foreach (var caller in new[] { "Order", "Cart", "Payment" })
                options.AddPolicy(caller + "Service", policy => policy.AddAuthenticationSchemes(ServiceScheme).RequireAuthenticatedUser().RequireClaim("service", caller));
            options.AddPolicy("OrderReaders", policy => policy.AddAuthenticationSchemes(ServiceScheme).RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.HasClaim("service", "Cart") || context.User.HasClaim("service", "Payment")));
            options.AddPolicy("InventoryOperations", policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ServiceScheme)
                .RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("service", "Order")));
        });
        return services;
    }
    public static Guid CustomerId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("User identity is missing.");
    public static string OutgoingKey(IConfiguration config, string caller) =>
        config["ServiceAuth:OutgoingKey"] ?? config[$"ServiceAuth:Keys:{caller}"]
        ?? throw new InvalidOperationException($"Configure the {caller} service credential.");
}
public sealed class ServiceKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Service-Key", out var provided)) return Task.FromResult(AuthenticateResult.NoResult());
        if (provided.Count != 1 || string.IsNullOrEmpty(provided[0])) return Task.FromResult(AuthenticateResult.Fail("Invalid service credential."));
        var candidate = SHA256.HashData(Encoding.UTF8.GetBytes(provided[0]!));
        foreach (var item in configuration.GetSection("ServiceAuth:Keys").GetChildren())
        {
            if (string.IsNullOrWhiteSpace(item.Value)) continue;
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes(item.Value));
            if (!CryptographicOperations.FixedTimeEquals(candidate, expected)) continue;
            var identity = new ClaimsIdentity([new Claim("service", item.Key)], SecurityExtensions.ServiceScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
        return Task.FromResult(AuthenticateResult.Fail("Invalid service credential."));
    }
}
