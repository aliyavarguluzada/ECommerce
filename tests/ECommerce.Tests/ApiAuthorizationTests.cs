using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests;

public sealed class ApiAuthorizationTests
{
    [Theory]

    [InlineData("expired")]

    [InlineData("issuer")]

    [InlineData("audience")]

    [InlineData("signature")]

    [InlineData("subject")]

    public async Task Invalid_access_token_is_rejected(string defect)
    {
        await using var factory = new ApiFactory<ECommerce.Cart.Api.Program>();

        using var client = factory.Client(authenticated: false);

        var token = TestCredentials.Token(
            user: defect == "subject" ? Guid.Empty : null,
            expired: defect == "expired",
            issuer: defect == "issuer" ? "wrong" : "ecommerce-identity",
            audience: defect == "audience" ? "wrong" : "ecommerce",
            otherKey: defect == "signature");

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);

    }

    [Theory]

    [InlineData("cart")]

    [InlineData("order")]

    [InlineData("payment")]

    [InlineData("identity")]

    public async Task Customer_endpoints_require_authentication(string service)
    {
        var world = new CommerceWorld();

        if (service == "cart")
        {
            await using var f = new ApiFactory<ECommerce.Cart.Api.Program>(s => FakeApiServices.Cart(s, world));

            using var c = f.Client(authenticated: false);

            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/cart")).StatusCode);

        }

        if (service == "order")
        {
            await using var f = new ApiFactory<ECommerce.Order.Api.Program>(s => FakeApiServices.Order(s, world));

            using var c = f.Client(authenticated: false);

            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/orders")).StatusCode);

        }

        if (service == "payment")
        {
            await using var f = new ApiFactory<ECommerce.Payment.Api.Program>(s => FakeApiServices.Payment(s, world));

            using var c = f.Client(authenticated: false);

            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/payments")).StatusCode);

        }

        if (service == "identity")
        {
            await using var f = new ApiFactory<ECommerce.Identity.Api.Program>(FakeApiServices.Identity);

            using var c = f.Client(authenticated: false);

            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/identity/me")).StatusCode);

        }

    }

    [Fact]

    public async Task Cart_is_derived_from_token_not_body_customer_id()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        await using var factory = new ApiFactory<ECommerce.Cart.Api.Program>(s => FakeApiServices.Cart(s, world));

        using var client = factory.Client();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync(
                $"/api/cart/items/{product}",
                new { quantity = 2, customerId = Guid.NewGuid() })).StatusCode);

        var result = await client.GetFromJsonAsync<JsonElement>("/api/cart");

        Assert.Equal(TestCredentials.Customer, result.GetProperty("customerId").GetGuid());

        using var another = factory.Client(Guid.NewGuid());

        Assert.Equal(
            0,
            (await another.GetFromJsonAsync<JsonElement>("/api/cart")).GetProperty("items").GetArrayLength());

    }

    [Fact]

    public async Task Order_creation_ignores_spoofed_customer_and_ownership_is_enforced()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        await using var factory = new ApiFactory<ECommerce.Order.Api.Program>(s => FakeApiServices.Order(s, world));

        using var client = factory.Client();

        var id = Guid.NewGuid();

        var created = await client.PostAsJsonAsync(
            "/api/orders",
            new {
                requestId = id,
                customerId = Guid.NewGuid(),
                shippingAddress = CommerceWorld.Address(),
                items = new[] { new { productId = product, quantity = 2 } } });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal(TestCredentials.Customer, world.OrderStore.Data.Orders[id].CustomerId);

        using var other = factory.Client(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Not mine" })).StatusCode);

        var list = await other.GetFromJsonAsync<JsonElement>($"/api/orders?customerId={TestCredentials.Customer}");

        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());

        using var admin = factory.Client(admin: true);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/orders/{id}")).StatusCode);

    }

    [Fact]

    public async Task Fulfillment_is_admin_only_and_unpaid_orders_still_cannot_ship()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        var order = await world.Order.CreateAsync(world.NewOrder(product), default);

        await using var factory = new ApiFactory<ECommerce.Order.Api.Program>(s => FakeApiServices.Order(s, world));

        using var customer = factory.Client();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await customer.PostAsJsonAsync(
                $"/api/orders/{order.Id}/ship",
                new { carrier = "Carrier", trackingNumber = "TRACK" })).StatusCode);

        using var admin = factory.Client(admin: true);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync(
                $"/api/orders/{order.Id}/ship",
                new { carrier = "Carrier", trackingNumber = "TRACK" })).StatusCode);

    }

    [Theory]

    [InlineData("none", HttpStatusCode.Unauthorized)]

    [InlineData("customer", HttpStatusCode.Forbidden)]

    [InlineData("admin", HttpStatusCode.OK)]

    public async Task Inventory_balances_require_admin(string role, HttpStatusCode expected)
    {
        var world = new CommerceWorld();

        await using var factory = new ApiFactory<ECommerce.Inventory.Api.Program>(
            s => FakeApiServices.Inventory(s, world));

        using var client = factory.Client(admin: role == "admin", authenticated: role != "none");

        Assert.Equal(expected, (await client.GetAsync("/api/inventory/stock")).StatusCode);

    }

    [Theory]

    [InlineData("invalid", HttpStatusCode.Unauthorized)]

    [InlineData("cart", HttpStatusCode.Forbidden)]

    [InlineData("order", HttpStatusCode.OK)]

    public async Task Inventory_service_operations_require_the_order_credential(string caller, HttpStatusCode expected)
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        await using var factory = new ApiFactory<ECommerce.Inventory.Api.Program>(
            s => FakeApiServices.Inventory(s, world));

        using var client = factory.Client(authenticated: false);

        TestCredentials.Service(
            client,
            caller == "order" ? TestCredentials.OrderKey : caller == "cart" ? TestCredentials.CartKey : "wrong-key");

        Assert.Equal(
            expected,
            (await client.PostAsJsonAsync(
                "/api/inventory/reservations",
                new {
                    orderId = Guid.NewGuid(),
                    items = new[] { new { productId = product, quantity = 1 } } })).StatusCode);

    }

    [Theory]

    [InlineData("customer", HttpStatusCode.Unauthorized)]

    [InlineData("order", HttpStatusCode.Forbidden)]

    [InlineData("cart", HttpStatusCode.OK)]

    [InlineData("payment", HttpStatusCode.OK)]

    public async Task Internal_order_reads_are_limited_to_intended_service_callers(
        string caller,
        HttpStatusCode expected)
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        var order = await world.Order.CreateAsync(world.NewOrder(product), default);

        await using var factory = new ApiFactory<ECommerce.Order.Api.Program>(s => FakeApiServices.Order(s, world));

        using var client = factory.Client(authenticated: caller == "customer");

        if (caller != "customer")
        {
            TestCredentials.Service(
                client,
                caller == "order" ? TestCredentials.OrderKey : caller == "cart" ? TestCredentials.CartKey : TestCredentials.PaymentKey);

        }

        var response = await client.GetAsync($"/api/orders/internal/{order.Id}");

        Assert.Equal(expected, response.StatusCode);

        if (expected == HttpStatusCode.OK)
        {
            Assert.DoesNotContain("shippingAddress", await response.Content.ReadAsStringAsync());

        }

    }

    [Fact]

    public async Task Payment_receipts_are_customer_owned_and_refund_route_is_not_public()
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        var order = await world.Order.CreateAsync(world.NewOrder(product), default);

        var receipt = await world.Payment.PayAsync(
            TestCredentials.Customer,
            new(Guid.NewGuid(), order.Id, "dev-approved"),
            default);

        await using var factory = new ApiFactory<ECommerce.Payment.Api.Program>(s => FakeApiServices.Payment(s, world));

        using var other = factory.Client(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/payments/{receipt.Id}")).StatusCode);

        Assert.Equal(
            0,
            (await other.GetFromJsonAsync<JsonElement>(
                $"/api/payments?customerId={TestCredentials.Customer}")).GetProperty("totalCount").GetInt32());

        using var admin = factory.Client(admin: true);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/payments/{receipt.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await admin.PostAsync($"/api/payments/internal/orders/{order.Id}/refund", null)).StatusCode);

    }

    [Fact]

    public async Task Identity_profile_uses_authenticated_subject()
    {
        await using var factory = new ApiFactory<ECommerce.Identity.Api.Program>(FakeApiServices.Identity);

        var id = Guid.NewGuid();

        using var client = factory.Client(id);

        Assert.Equal(id, (await client.GetFromJsonAsync<JsonElement>("/api/identity/me")).GetProperty("id").GetGuid());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync(
                $"/api/identity/users/{Guid.NewGuid()}/admin",
                new { isAdmin = true })).StatusCode);

    }

    [Theory]

    [InlineData("short", false)]

    [InlineData("alllowercase123!", false)]

    [InlineData("ALLUPPERCASE123!", false)]

    [InlineData("NoSpecialChars123", false)]

    [InlineData("NoDigitsHere!!", false)]

    [InlineData("Valid-Password123!", true)]

    public async Task Configured_identity_password_policy_is_enforced(string password, bool expected)
    {
        await using var factory = new ApiFactory<ECommerce.Identity.Api.Program>(FakeApiServices.Identity);

        using var scope = factory.Services.CreateScope();

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ECommerce.Identity.Infrastructure.ApplicationUser>>();

        var result = await manager.PasswordValidators[0].ValidateAsync(manager, new(), password);

        Assert.Equal(expected, result.Succeeded);

    }

    [Fact]

    public async Task Malformed_registration_and_missing_role_boolean_are_rejected_before_handlers()
    {
        await using var factory = new ApiFactory<ECommerce.Identity.Api.Program>(FakeApiServices.Identity);

        using var client = factory.Client(admin: true);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/api/identity/register",
                new { email = "invalid", password = "Password123!", displayName = "Name" })).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/identity/users/{Guid.NewGuid()}/admin", new { })).StatusCode);

    }

}
