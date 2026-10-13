using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Inventory.Application;
namespace ECommerce.Inventory.Infrastructure;

public sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<CatalogProduct> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"api/products/{id}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ServiceException(400, $"Catalog product {id} does not exist.");
            if (!response.IsSuccessStatusCode) throw new ServiceException(503, "Catalog is unavailable.");
            var product = await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken: ct)
                ?? throw new ServiceException(503, "Catalog returned an empty response.");
            if (product.Id != id || string.IsNullOrWhiteSpace(product.Currency) || string.IsNullOrWhiteSpace(product.Name))
                throw new ServiceException(503, "Catalog returned invalid product data.");
            return product;
        }
        catch (HttpRequestException) { throw new ServiceException(503, "Catalog is unavailable."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ServiceException(503, "Catalog request timed out."); }
        catch (JsonException) { throw new ServiceException(503, "Catalog returned invalid product data."); }
    }
}
