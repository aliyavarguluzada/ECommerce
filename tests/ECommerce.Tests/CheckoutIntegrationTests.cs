using System.Net;
using System.Net.Http.Json;
using ECommerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ord = ECommerce.Order.Application;
using Car = ECommerce.Cart.Application;
using Pay = ECommerce.Payment.Application;

namespace ECommerce.Tests;

public sealed class CheckoutIntegrationTests
{
    [Theory]

    [InlineData("dev-approved", HttpStatusCode.Created)]

    [InlineData("dev-declined", HttpStatusCode.PaymentRequired)]

    public async Task Cart_checkout_payment_and_fulfillment_cross_real_service_HTTP_clients(
        string method,
        HttpStatusCode expected)
    {
        var world = new CommerceWorld();

        var product = world.AddProduct();

        ApiFactory<ECommerce.Order.Api.Program>? orders = null;

        ApiFactory<ECommerce.Payment.Api.Program>? payments = null;

        await using var inventory = new ApiFactory<ECommerce.Inventory.Api.Program>(
            services => FakeApiServices.Inventory(services, world));

        using var inventoryHttp = Backend(() => inventory, TestCredentials.OrderKey);

        using var refundsHttp = Backend(() => payments!, TestCredentials.OrderKey);

        using var paymentOrdersHttp = Backend(() => orders!, TestCredentials.PaymentKey);

        using var cartOrdersHttp = Backend(() => orders!, TestCredentials.CartKey);

        orders = new(services =>
        {
            services.RemoveAll<Ord.IOrderService>();

            services.AddSingleton<Ord.IOrderService>(
                new OrderTestService(
                    world.Catalog,
                    new ECommerce.Order.Infrastructure.InventoryClient(inventoryHttp),
                    new ECommerce.Order.Infrastructure.PaymentClient(refundsHttp),
                    world.OrderStore.State));

        });

        payments = new(services =>
        {
            services.RemoveAll<Pay.IPaymentService>();

            services.AddSingleton<Pay.IPaymentService>(
                new PaymentTestService(
                    new ECommerce.Payment.Infrastructure.OrderClient(paymentOrdersHttp),
                    world.Provider,
                    world.PaymentStore.State));

        });

        await using var orderLifetime = orders;

        await using var paymentLifetime = payments;

        await using var cart = new ApiFactory<ECommerce.Cart.Api.Program>(services =>
        {
            services.RemoveAll<Car.ICartService>();

            services.AddSingleton<Car.ICartService>(
                new CartTestService(
                    world.Catalog,
                    new ECommerce.Cart.Infrastructure.OrderClient(cartOrdersHttp),
                    world.CartStore.State));

        });

        using var cartClient = cart.Client();

        using var paymentClient = payments.Client();

        using var orderClient = orders.Client(admin: true);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await cartClient.PutAsJsonAsync($"/api/cart/items/{product}", new { quantity = 2 })).StatusCode);

        var checkout = new Car.CheckoutRequest(Guid.NewGuid(), CommerceWorld.CartAddress());

        var checkoutResponse = await cartClient.PostAsJsonAsync("/api/cart/checkout", checkout);

        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);

        var snapshot = (await checkoutResponse.Content.ReadFromJsonAsync<Car.OrderSnapshot>())!;

        Assert.Equal(checkout.RequestId, snapshot.Id);

        Assert.Equal(20m, snapshot.Total);

        Assert.Empty((await cartClient.GetFromJsonAsync<Car.CartResponse>("/api/cart"))!.Items);

        var pay = new Pay.PayRequest(Guid.NewGuid(), snapshot.Id, method);

        Assert.Equal(expected, (await paymentClient.PostAsJsonAsync("/api/payments", pay)).StatusCode);

        Assert.Equal(expected, (await paymentClient.PostAsJsonAsync("/api/payments", pay)).StatusCode);

        Assert.Equal(1, world.Provider.ChargeCount);

        if (method == "dev-approved")
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await orderClient.PostAsJsonAsync(
                    $"/api/orders/{snapshot.Id}/ship",
                    new { carrier = "Test carrier", trackingNumber = "TRACK-123" })).StatusCode);

            Assert.Equal(
                HttpStatusCode.OK,
                (await orderClient.PostAsync($"/api/orders/{snapshot.Id}/deliver", null)).StatusCode);

            Assert.Equal("Delivered", world.OrderStore.Data.Orders[snapshot.Id].Status.ToString());

            Assert.Equal(98, world.InventoryStore.Data.Stocks[product].OnHand);

        }

        else
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await orderClient.PostAsJsonAsync(
                    $"/api/orders/{snapshot.Id}/cancel",
                    new { reason = "Declined" })).StatusCode);

            Assert.Equal(100, world.InventoryStore.Data.Stocks[product].Available);

        }

    }

    private static HttpClient Backend<T>(Func<ApiFactory<T>> factory, string key)

        where T : class
    {
        var client = new HttpClient(new BackendHandler<T>(factory))
        {
            BaseAddress = new("https://localhost/")

        };

        TestCredentials.Service(client, key);

        return client;
    }

    private sealed class BackendHandler<T>(Func<ApiFactory<T>> factory) : HttpMessageHandler where T : class
    {
        private HttpMessageInvoker? _server;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct) => (_server ??= new HttpMessageInvoker(
            factory().Server.CreateHandler())).SendAsync(
            request,
            ct);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _server?.Dispose();

            }

            base.Dispose(disposing);

        }

    }

}
