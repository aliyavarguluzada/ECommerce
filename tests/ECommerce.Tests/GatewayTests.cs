using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ECommerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Forwarder;

namespace ECommerce.Tests;

public sealed class GatewayTests
{
    [Theory]

    [InlineData("/api/products", false, false, HttpStatusCode.OK)]

    [InlineData("/api/categories", false, false, HttpStatusCode.OK)]

    [InlineData("/api/identity/login", false, false, HttpStatusCode.OK)]

    [InlineData("/api/cart", false, false, HttpStatusCode.Unauthorized)]

    [InlineData("/api/orders", true, false, HttpStatusCode.OK)]

    [InlineData("/api/payments", true, false, HttpStatusCode.OK)]

    [InlineData("/api/inventory/stock", true, false, HttpStatusCode.Forbidden)]

    [InlineData("/api/inventory/stock", true, true, HttpStatusCode.OK)]

    public async Task Gateway_routes_and_authorization(
        string path,
        bool authenticated,
        bool admin,
        HttpStatusCode expected)
    {
        var forwarder = new RecordingForwarder();

        await using var factory = new ApiFactory<ECommerce.ApiGateway.Program>(services =>
        {
            services.RemoveAll<IForwarderHttpClientFactory>();

            services.AddSingleton<IForwarderHttpClientFactory>(forwarder);

        });

        using var client = factory.Client(admin: admin, authenticated: authenticated);

        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);

        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, forwarder.Handler.Count);

        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal(path, forwarder.Handler.Path);

        }

    }

    [Theory]

    [InlineData("/api/orders/internal/test")]

    [InlineData("/api/payments/internal/orders/test/refund")]

    [InlineData("/api/orders/%69nternal/test")]

    [InlineData("/api/orders/INTERNAL/test")]

    public async Task Internal_routes_are_never_forwarded(string path)
    {
        var forwarder = new RecordingForwarder();

        await using var factory = new ApiFactory<ECommerce.ApiGateway.Program>(services =>
        {
            services.RemoveAll<IForwarderHttpClientFactory>();

            services.AddSingleton<IForwarderHttpClientFactory>(forwarder);

        });

        using var client = factory.Client(admin: true);

        TestCredentials.Service(client, TestCredentials.OrderKey);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);

        Assert.Equal(0, forwarder.Handler.Count);

    }

    [Fact]

    public async Task Service_and_user_spoofing_headers_are_removed_but_bearer_token_is_forwarded()
    {
        var forwarder = new RecordingForwarder();

        await using var factory = new ApiFactory<ECommerce.ApiGateway.Program>(services =>
        {
            services.RemoveAll<IForwarderHttpClientFactory>();

            services.AddSingleton<IForwarderHttpClientFactory>(forwarder);

        });

        using var client = factory.Client();

        TestCredentials.Service(client, TestCredentials.OrderKey);

        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/cart")).StatusCode);

        Assert.False(forwarder.Handler.ServiceHeader);

        Assert.False(forwarder.Handler.UserHeader);

        Assert.StartsWith("Bearer ", forwarder.Handler.Authorization);

    }

    private sealed class RecordingForwarder : IForwarderHttpClientFactory
    {
        public RecordingHandler Handler { get; } = new();

        public HttpMessageInvoker CreateClient(
            ForwarderHttpClientContext context) => new(
            Handler,
            disposeHandler: false);

    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }

        public string? Path { get; private set; }

        public bool ServiceHeader { get; private set; }

        public bool UserHeader { get; private set; }

        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++;

            Path = request.RequestUri!.AbsolutePath;

            ServiceHeader = request.Headers.Contains("X-Service-Key");

            UserHeader = request.Headers.Contains("X-User-Id");

            Authorization = request.Headers.Authorization?.ToString();

            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.OK) {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }

    }

}
