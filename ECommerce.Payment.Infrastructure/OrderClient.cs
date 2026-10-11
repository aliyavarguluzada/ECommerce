using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Payment.Application;
namespace ECommerce.Payment.Infrastructure;
public sealed class OrderClient(HttpClient http) : IOrderClient
{
    public async Task<OrderSnapshot> GetAsync(Guid id, CancellationToken ct)
    {
        var value = await SendAsync(HttpMethod.Get, $"api/orders/internal/{id}", null, ct);
        try { return JsonSerializer.Deserialize<OrderSnapshot>(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ServiceException(503, "Order response is empty."); }
        catch (JsonException) { throw new ServiceException(503, "Order response is invalid."); }
    }
    public async Task RecordPaidAsync(Guid id, Guid paymentId, decimal amount, string currency, CancellationToken ct) =>
        _ = await SendAsync(HttpMethod.Post, $"api/orders/internal/{id}/payment", new { PaymentId = paymentId, Amount = amount, Currency = currency }, ct);
    private async Task<string> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new ServiceException(response.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 :
                response.StatusCode == System.Net.HttpStatusCode.Conflict ? 409 : 503, "Order is unavailable or not in the required state.");
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException) { throw new ServiceException(503, "Order is unavailable."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ServiceException(503, "Order request timed out."); }
    }
}
