using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Order.Application;
namespace ECommerce.Order.Infrastructure;
public sealed class PaymentClient(HttpClient http) : IPaymentClient
{
    public async Task<RefundSnapshot> RefundAsync(Guid orderId, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync($"api/payments/internal/orders/{orderId}/refund", null, ct);
            if (!response.IsSuccessStatusCode) throw new ServiceException(503, "Payment refund remains pending recovery.");
            return await response.Content.ReadFromJsonAsync<RefundSnapshot>(cancellationToken: ct)
                ?? throw new ServiceException(503, "Payment returned no refund result.");
        }
        catch (HttpRequestException) { throw new ServiceException(503, "Payment is unavailable."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ServiceException(503, "Payment refund request timed out."); }
        catch (JsonException) { throw new ServiceException(503, "Payment returned invalid refund data."); }
    }
}
