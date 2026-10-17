using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Cart.Application;
using ECommerce.Cart.Domain;

namespace ECommerce.Cart.Infrastructure;

public sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<CatalogProduct?> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"api/products/{id}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceException(503, "Catalog is unavailable.");

            }

            var product = await response.Content.ReadFromJsonAsync<CatalogProduct>(
                cancellationToken: ct) ?? throw new ServiceException(
                503,
                "Catalog returned no product.");

            if (product.Id != id || product.Price < 0 || product.Price > 9999999999999999.99m || decimal.Round(
                product.Price,
                2) != product.Price || string.IsNullOrWhiteSpace(
                product.Currency) || product.Currency.Length != 3)
            {
                throw new ServiceException(503, "Catalog returned invalid product data.");

            }

            return product;
        }

        catch (HttpRequestException)
        {
            throw new ServiceException(503, "Catalog is unavailable.");

        }

        catch (TaskCanceledException)when (!ct.IsCancellationRequested)
        {
            throw new ServiceException(503, "Catalog timed out.");

        }

        catch (JsonException)
        {
            throw new ServiceException(503, "Catalog returned invalid data.");

        }

    }

}

public sealed class OrderClient(HttpClient http) : IOrderClient
{
    public Task<OrderSnapshot> CreateAsync(
        Guid id,
        Guid customer,
        AddressRequest address,
        IEnumerable<CartLine> items,
        CancellationToken ct) => SendAsync(
        HttpMethod.Post,
        "api/orders/internal",
        new {
            RequestId = id,
            CustomerId = customer,
            ShippingAddress = address,
            Items = items.Select(x => new { x.ProductId, x.Quantity }) },
        ct);

    public Task<OrderSnapshot> GetAsync(
        Guid id,
        CancellationToken ct) => SendAsync(
        HttpMethod.Get,
        $"api/orders/internal/{id}",
        null,
        ct);

    private async Task<OrderSnapshot> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);

            }

            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceException(
                    (int)response.StatusCode is 400 or 409 ? (int)response.StatusCode : 503,
                    "Order could not complete checkout. Inspect the cart and retry the original checkout request.");

            }

            return await response.Content.ReadFromJsonAsync<OrderSnapshot>(
                cancellationToken: ct) ?? throw new ServiceException(
                503,
                "Order returned no checkout result.");
        }

        catch (HttpRequestException)
        {
            throw new ServiceException(503, "Order is unavailable. Retry checkout with the same request ID.");

        }

        catch (TaskCanceledException)when (!ct.IsCancellationRequested)
        {
            throw new ServiceException(503, "Order timed out. Retry checkout with the same request ID.");

        }

        catch (JsonException)
        {
            throw new ServiceException(503, "Order returned invalid data.");

        }

    }

}
