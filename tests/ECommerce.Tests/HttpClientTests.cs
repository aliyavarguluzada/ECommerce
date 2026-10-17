using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ECommerce.Tests;

public sealed class HttpClientTests
{
    [Theory]

    [InlineData("Inventory", "network")]

    [InlineData("Order", "network")]

    [InlineData("Cart", "network")]

    [InlineData("Inventory", "timeout")]

    [InlineData("Order", "timeout")]

    [InlineData("Cart", "timeout")]

    [InlineData("Inventory", "json")]

    [InlineData("Order", "json")]

    [InlineData("Cart", "json")]

    [InlineData("Inventory", "wrongId")]

    [InlineData("Order", "wrongId")]

    [InlineData("Cart", "wrongId")]

    [InlineData("Inventory", "server")]

    [InlineData("Order", "server")]

    [InlineData("Cart", "server")]

    public async Task Catalog_dependency_failures_become_503(string service, string defect)
    {
        using var http = Client(defect);

        var id = Guid.NewGuid();

        switch (service)
        {
            case "Inventory":

                Assert.Equal(
                    503,
                    (await Assert.ThrowsAsync<ECommerce.Inventory.Application.ServiceException>(
                        () => new ECommerce.Inventory.Infrastructure.CatalogClient(http).GetAsync(
                            id,
                            default))).StatusCode);

                break;

            case "Order":

                Assert.Equal(
                    503,
                    (await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(
                        () => new ECommerce.Order.Infrastructure.CatalogClient(http).GetAsync(
                            id,
                            default))).StatusCode);

                break;

            case "Cart":

                Assert.Equal(
                    503,
                    (await Assert.ThrowsAsync<ECommerce.Cart.Application.ServiceException>(
                        () => new ECommerce.Cart.Infrastructure.CatalogClient(http).GetAsync(id, default))).StatusCode);

                break;

        }

    }

    [Fact]

    public async Task Missing_product_is_invalid_for_inventory_and_order_but_visible_as_unavailable_in_cart()
    {
        using var http = Client("missing");

        var id = Guid.NewGuid();

        Assert.Equal(
            400,
            (await Assert.ThrowsAsync<ECommerce.Inventory.Application.ServiceException>(
                () => new ECommerce.Inventory.Infrastructure.CatalogClient(http).GetAsync(id, default))).StatusCode);

        Assert.Equal(
            400,
            (await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(
                () => new ECommerce.Order.Infrastructure.CatalogClient(http).GetAsync(id, default))).StatusCode);

        Assert.Null(await new ECommerce.Cart.Infrastructure.CatalogClient(http).GetAsync(id, default));

    }

    [Theory]

    [InlineData(400)]

    [InlineData(404)]

    [InlineData(409)]

    public async Task Inventory_rejection_preserves_problem_detail_as_conflict(int status)
    {
        using var http = new HttpClient(
            new ReplyHandler(
                _ => new(
                    (HttpStatusCode)status) {
                    Content = JsonContent.Create(new { detail = "Insufficient stock" }) }))
        {
            BaseAddress = new("https://inventory/")

        };

        var error = await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(
            () => new ECommerce.Order.Infrastructure.InventoryClient(http).ReserveAsync(Guid.NewGuid(), [], default));

        Assert.Equal(409, error.StatusCode);

        Assert.Equal("Insufficient stock", error.Message);

    }

    [Theory]

    [InlineData("Reserved")]

    [InlineData("Committed")]

    [InlineData("Released")]

    public async Task Inventory_reservation_contract_accepts_named_statuses(string status)
    {
        var id = Guid.NewGuid();

        using var http = new HttpClient(
            new ReplyHandler(
                _ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { OrderId = id, Status = status }) }))
        {
            BaseAddress = new("https://inventory/")

        };

        var response = await new ECommerce.Order.Infrastructure.InventoryClient(http).ReserveAsync(id, [], default);

        Assert.Equal(id, response.OrderId);

        Assert.Equal(status, response.Status);

    }

    [Fact]

    public async Task Inventory_invalid_status_is_not_accepted_as_success()
    {
        var id = Guid.NewGuid();

        using var http = new HttpClient(
            new ReplyHandler(
                _ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { OrderId = id, Status = "Unknown" }) }))
        {
            BaseAddress = new("https://inventory/")

        };

        Assert.Equal(
            503,
            (await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(
                () => new ECommerce.Order.Infrastructure.InventoryClient(http).CommitAsync(id, default))).StatusCode);

    }

    [Theory]

    [InlineData(404, 404)]

    [InlineData(409, 409)]

    [InlineData(500, 503)]

    [InlineData(401, 503)]

    public async Task Payment_order_client_maps_unavailable_and_business_errors(int upstream, int expected)
    {
        using var http = new HttpClient(new ReplyHandler(_ => new((HttpStatusCode)upstream)))
        {
            BaseAddress = new("https://order/")

        };

        Assert.Equal(
            expected,
            (await Assert.ThrowsAsync<ECommerce.Payment.Application.ServiceException>(
                () => new ECommerce.Payment.Infrastructure.OrderClient(http).GetAsync(
                    Guid.NewGuid(),
                    default))).StatusCode);

    }

    [Theory]

    [InlineData(400, 400)]

    [InlineData(409, 409)]

    [InlineData(500, 503)]

    public async Task Cart_order_client_preserves_definite_rejections_for_unfreezing(int upstream, int expected)
    {
        using var http = new HttpClient(new ReplyHandler(_ => new((HttpStatusCode)upstream)))
        {
            BaseAddress = new("https://order/")

        };

        Assert.Equal(
            expected,
            (await Assert.ThrowsAsync<ECommerce.Cart.Application.ServiceException>(
                () => new ECommerce.Cart.Infrastructure.OrderClient(http).GetAsync(
                    Guid.NewGuid(),
                    default))).StatusCode);

    }

    [Fact]

    public async Task Refund_outage_remains_recoverable()
    {
        using var http = Client("network");

        Assert.Equal(
            503,
            (await Assert.ThrowsAsync<ECommerce.Order.Application.ServiceException>(
                () => new ECommerce.Order.Infrastructure.PaymentClient(http).RefundAsync(
                    Guid.NewGuid(),
                    default))).StatusCode);

    }

    private static HttpClient Client(string defect) => new(new ReplyHandler(_ => defect switch
    {
        "network" => throw new HttpRequestException(),

        "timeout" => throw new TaskCanceledException(),

        "missing" => new(HttpStatusCode.NotFound),

        "server" => new(HttpStatusCode.InternalServerError),

        "json" => new(HttpStatusCode.OK) { Content = new StringContent("not-json", Encoding.UTF8, "application/json") },

        "wrongId" => new(
            HttpStatusCode.OK) {
            Content = JsonContent.Create(
                new { Id = Guid.NewGuid(), Name = "Mouse", Price = 10m, Currency = "USD", IsActive = true }) },

        _ => throw new ArgumentException(defect)}))
    {
        BaseAddress = new("https://dependency/")

    };

    private sealed class ReplyHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct) => Task.FromResult(
            reply(request));

    }

}
