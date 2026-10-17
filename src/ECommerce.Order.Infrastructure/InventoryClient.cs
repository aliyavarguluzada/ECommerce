using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Order.Application;

namespace ECommerce.Order.Infrastructure;

public sealed class InventoryClient(HttpClient http) : IInventoryClient
{
    public Task<ReservationSnapshot> ReserveAsync(
        Guid id,
        IEnumerable<OrderItemRequest> items,
        CancellationToken ct) => SendAsync(
        HttpMethod.Post,
        "api/inventory/reservations",
        new { OrderId = id, Items = items },
        id,
        ct);

    public Task<ReservationSnapshot> CommitAsync(
        Guid id,
        CancellationToken ct) => SendAsync(
        HttpMethod.Post,
        $"api/inventory/reservations/{id}/commit",
        null,
        id,
        ct);

    public Task<ReservationSnapshot> ReleaseAsync(
        Guid id,
        CancellationToken ct) => SendAsync(
        HttpMethod.Post,
        $"api/inventory/reservations/{id}/release",
        null,
        id,
        ct);

    private async Task<ReservationSnapshot> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        Guid id,
        CancellationToken ct)
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
                if ((int)response.StatusCode is 400 or 404 or 409)
                {
                    var message = "Inventory rejected the stock operation.";

                    try
                    {
                        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

                        if (problem.RootElement.TryGetProperty(
                            "detail",
                            out var detail) && detail.GetString()is { } text)
                        {
                            message = text;

                        }

                    }

                    catch (JsonException)
                    {
                    }

                    throw new ServiceException(409, message);

                }

                throw new ServiceException(503, "Inventory is unavailable.");

            }

            var reservation = await response.Content.ReadFromJsonAsync<ReservationSnapshot>(
                cancellationToken: ct) ?? throw new ServiceException(
                503,
                "Inventory returned an empty response.");

            if (reservation.OrderId != id || reservation.Status is not ("Reserved" or "Committed" or "Released"))
            {
                throw new ServiceException(503, "Inventory returned an invalid reservation.");

            }

            return reservation;
        }

        catch (HttpRequestException)
        {
            throw new ServiceException(503, "Inventory is unavailable.");

        }

        catch (TaskCanceledException)when (!ct.IsCancellationRequested)
        {
            throw new ServiceException(503, "Inventory request timed out.");

        }

        catch (JsonException)
        {
            throw new ServiceException(503, "Inventory returned invalid reservation data.");

        }

    }

}
