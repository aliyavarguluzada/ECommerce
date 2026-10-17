using System.Net;
using System.Text;
using System.Text.Json;
using ECommerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests;

public sealed class OpenApiTests
{
    [Theory]

    [InlineData("catalog")]

    [InlineData("identity")]

    [InlineData("cart")]

    [InlineData("order")]

    [InlineData("inventory")]

    [InlineData("payment")]

    public Task Protected_endpoints_document_bearer_authentication(string service) => service switch
    {
        "catalog" => VerifyAsync<ECommerce.Catalog.Api.Program>("/api/products", "post", "/api/products", "get"),

        "identity" => VerifyAsync<ECommerce.Identity.Api.Program>(
            "/api/identity/me",
            "get",
            "/api/identity/login",
            "post"),

        "cart" => VerifyAsync<ECommerce.Cart.Api.Program>("/api/cart", "get"),

        "order" => VerifyAsync<ECommerce.Order.Api.Program>("/api/orders", "get"),

        "inventory" => VerifyAsync<ECommerce.Inventory.Api.Program>("/api/inventory/stock", "get"),

        "payment" => VerifyAsync<ECommerce.Payment.Api.Program>("/api/payments", "get"),

        _ => throw new ArgumentException("Unknown API.")};

    private static async Task VerifyAsync<TProgram>(
        string path,
        string method,
        string? publicPath = null,
        string? publicMethod = null)

        where TProgram : class
    {
        await using var factory = new ApiFactory<TProgram>();

        using var client = factory.Client(authenticated: false);

        using var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "bearer",
            document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());

        Assert.True(
            document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("security")[0].TryGetProperty(
                "Bearer",
                out _));

        if (publicPath is not null)
        {
            var operation = document.RootElement.GetProperty("paths").GetProperty(publicPath).GetProperty(
                publicMethod!);

            Assert.True(!operation.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);

        }

    }

    [Fact]

    public async Task Internal_endpoints_require_service_keys_in_documentation()
    {
        await using var factory = new ApiFactory<ECommerce.Order.Api.Program>();

        using var client = factory.Client(authenticated: false);

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/orders/internal").GetProperty("post");

        Assert.True(operation.GetProperty("security")[0].TryGetProperty("ServiceKey", out _));

    }

    [Fact]

    public async Task Gateway_docs_use_same_origin_and_exclude_internal_operations()
    {
        await using var factory = new ApiFactory<ECommerce.ApiGateway.Program>(
            services => services.AddHttpClient("documentation").ConfigurePrimaryHttpMessageHandler(
                () => new DocumentationHandler()));

        using var client = factory.Client(authenticated: false);

        using var document = JsonDocument.Parse(await client.GetStringAsync("/docs/order/openapi.json"));

        var root = document.RootElement;

        Assert.Equal("/", root.GetProperty("servers")[0].GetProperty("url").GetString());

        Assert.True(root.GetProperty("paths").TryGetProperty("/api/orders", out _));

        Assert.False(root.GetProperty("paths").TryGetProperty("/api/orders/internal", out _));

        Assert.False(root.GetProperty("paths").TryGetProperty("/api/reservations", out _));

        Assert.False(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("ServiceKey", out _));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/docs/unknown/openapi.json")).StatusCode);

    }

    [Fact]

    public async Task Storefront_is_served_with_a_restrictive_content_policy()
    {
        await using var factory = new ApiFactory<ECommerce.ApiGateway.Program>();

        using var client = factory.Client(authenticated: false);

        using var response = await client.GetAsync("/shop/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains("Everyday, considered.", await response.Content.ReadAsStringAsync());

        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());

        using var module = await client.GetAsync("/shop/commerce.mjs");

        Assert.Equal(HttpStatusCode.OK, module.StatusCode);

    }

    private sealed class DocumentationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = "{\"servers\":[{\"url\":\"http://order:8080\"}],\"components\":{\"securitySchemes\":{\"Bearer\":{},\"ServiceKey\":{}}},\"paths\":{\"/api/orders\":{\"get\":{}},\"/api/orders/internal\":{\"post\":{}},\"/api/reservations\":{\"post\":{\"security\":[{\"ServiceKey\":[]}]}}}}";

            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.OK) {
                    Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }

    }

}
