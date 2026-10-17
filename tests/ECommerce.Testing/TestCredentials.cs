using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Testing;

public static class TestCredentials
{
    private static readonly RSA Key = RSA.Create(3072);

    public static readonly Guid Customer = Guid.NewGuid();

    public static readonly string OrderKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static readonly string CartKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static readonly string PaymentKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    static TestCredentials()
    {
        // These variables belong only to the test process, never the machine or user profile.

        Environment.SetEnvironmentVariable("Auth__PublicKey", Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo()));

        Environment.SetEnvironmentVariable("Auth__PrivateKey", Convert.ToBase64String(Key.ExportPkcs8PrivateKey()));

        Environment.SetEnvironmentVariable("ServiceAuth__Keys__Order", OrderKey);

        Environment.SetEnvironmentVariable("ServiceAuth__Keys__Cart", CartKey);

        Environment.SetEnvironmentVariable("ServiceAuth__Keys__Payment", PaymentKey);

        Environment.SetEnvironmentVariable("OrderRecovery__Enabled", "false");

        Environment.SetEnvironmentVariable("PaymentRecovery__Enabled", "false");

    }

    public static void Initialize()
    {
    }

    public static string Token(
        Guid? user = null,
        bool admin = false,
        bool customerRole = true,
        string issuer = "ecommerce-identity",
        string audience = "ecommerce",
        bool expired = false,
        bool otherKey = false)
    {
        var claims = new List<Claim>
        {
            new("sub", (user ?? Customer).ToString()),

            new("jti", Guid.NewGuid().ToString())

        };

        if (customerRole)
        {
            claims.Add(new("role", "Customer"));

        }

        if (admin)
        {
            claims.Add(new("role", "Admin"));

        }

        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            now.AddHours(-2),
            expired ? now.AddMinutes(-5) : now.AddMinutes(15),
            new SigningCredentials(
                new RsaSecurityKey(otherKey ? RSA.Create(3072) : Key),
                SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void Authenticate(
        HttpClient client,
        Guid? user = null,
        bool admin = false) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Bearer",
        Token(user, admin));

    public static void Service(HttpClient client, string key) => client.DefaultRequestHeaders.Add("X-Service-Key", key);

}
