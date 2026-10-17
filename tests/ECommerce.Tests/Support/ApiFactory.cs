using ECommerce.Identity.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ECommerce.Tests.Support;

public sealed class ApiFactory<TProgram>(
    Action<IServiceCollection>? configure = null) : WebApplicationFactory<TProgram> where TProgram : class
{
    public ApiFactory() : this(null)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestCredentials.Initialize();

        builder.UseEnvironment("Development");

        builder.UseContentRoot(
            Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "../../../../../src",
                    typeof(TProgram).Assembly.GetName().Name!)));

        builder.ConfigureLogging(logging => logging.ClearProviders());

        if (configure is not null)
        {
            builder.ConfigureServices(configure);

        }

    }

    public HttpClient Client(Guid? user = null, bool admin = false, bool authenticated = true)
    {
        TestCredentials.Initialize();

        var client = CreateClient(
            new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false });

        if (authenticated)
        {
            TestCredentials.Authenticate(client, user, admin);

        }

        return client;
    }

}

public static class FakeApiServices
{
    public static void Inventory(
        IServiceCollection services,
        CommerceWorld world) => Replace<ECommerce.Inventory.Application.IInventoryService>(
        services,
        world.Inventory);

    public static void Order(IServiceCollection services, CommerceWorld world) => Replace<ECommerce.Order.Application.IOrderService>(services, world.Order);

    public static void Cart(IServiceCollection services, CommerceWorld world) => Replace<ECommerce.Cart.Application.ICartService>(services, world.Cart);

    public static void Payment(IServiceCollection services, CommerceWorld world) => Replace<ECommerce.Payment.Application.IPaymentService>(services, world.Payment);

    public static void Identity(
        IServiceCollection services) => Replace<IIdentityService>(
        services,
        new IdentityApiStub());

    private static void Replace<T>(IServiceCollection services, T instance)

        where T : class
    {
        services.RemoveAll<T>();

        services.AddSingleton(instance);

    }

}

// Used only for HTTP binding/claims/policy tests. Real Identity persistence is covered

// separately by PostgreSQL tests, not represented by this stub.

internal sealed class IdentityApiStub : IIdentityService
{
    public Task<TokenResponse> RegisterAsync(
        RegisterRequest command,
        CancellationToken ct) => Task.FromResult(
        Tokens(command.Email));

    public Task<TokenResponse> LoginAsync(
        LoginRequest command,
        CancellationToken ct) => Task.FromResult(
        Tokens(command.Email));

    public Task<TokenResponse> RefreshAsync(
        string token,
        CancellationToken ct) => Task.FromResult(
        Tokens("test@example.com"));

    public Task LogoutAsync(Guid user, string token, CancellationToken ct) => Task.CompletedTask;

    public Task LogoutAllAsync(Guid user, CancellationToken ct) => Task.CompletedTask;

    public Task<UserProfile> GetAsync(Guid user, CancellationToken ct) => Task.FromResult(Profile(user));

    public Task<UserProfile> UpdateProfileAsync(
        Guid user,
        ProfileRequest command,
        CancellationToken ct) => Task.FromResult(
        Profile(user)with { DisplayName = command.DisplayName });

    public Task ChangePasswordAsync(
        Guid user,
        ChangePasswordRequest command,
        CancellationToken ct) => Task.CompletedTask;

    public Task<UserProfile> SetAdminAsync(
        Guid actor,
        Guid user,
        bool admin,
        CancellationToken ct) => Task.FromResult(
        Profile(user));

    private static UserProfile Profile(Guid user) => new(user, "test@example.com", "Test customer", ["Customer"], true);

    private static TokenResponse Tokens(
        string email) => new(
        TestCredentials.Token(),
        "test-refresh",
        DateTime.UtcNow.AddMinutes(15),
        Profile(TestCredentials.Customer)with { Email = email });

}
