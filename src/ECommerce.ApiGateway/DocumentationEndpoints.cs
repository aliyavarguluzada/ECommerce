using System.Text.Json.Nodes;

namespace ECommerce.ApiGateway;

public static class DocumentationEndpoints
{
    public static readonly string[] Services = ["catalog", "identity", "cart", "order", "inventory", "payment"];

    public static void MapServiceDocumentation(this WebApplication app)
    {
        app.MapGet("/docs/{service}/openapi.json", async (
            string service,
            IHttpClientFactory clients,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!Services.Contains(service, StringComparer.Ordinal))
            {
                return Results.NotFound();
            }

            var destination = configuration[$"ReverseProxy:Clusters:{service}:Destinations:primary:Address"];

            if (!Uri.TryCreate(destination, UriKind.Absolute, out var address))
            {
                return Results.Problem("The documentation service is not configured.", statusCode: 503);
            }

            try
            {
                using var response = await clients.CreateClient("documentation").GetAsync(
                    new Uri(address, "openapi/v1.json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    return Results.Problem(
                        "Start the service in Development to load its API documentation.",
                        statusCode: 503);
                }

                var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!.AsObject();

                document["servers"] = new JsonArray(new JsonObject { ["url"] = "/" });

                if (document["paths"] is JsonObject paths)
                {
                    foreach (var path in paths.Select(
                        pair => pair.Key).Where(
                        path => !path.StartsWith(
                            "/api/",
                            StringComparison.Ordinal) || path.Split('/').Contains(
                            "internal",
                            StringComparer.OrdinalIgnoreCase)).ToArray())
                    {
                        paths.Remove(path);

                    }

                    foreach (var path in paths.ToArray())
                    {
                        if (path.Value is not JsonObject operations)
                        {
                            continue;

                        }

                        foreach (var operation in operations.ToArray())
                        {
                            if (operation.Value is JsonObject operationObject && operationObject["security"] is JsonArray requirements && requirements.Any(
                                requirement => requirement is JsonObject item && item.ContainsKey("ServiceKey")))
                            {
                                operations.Remove(operation.Key);

                            }

                        }

                        if (operations.Count == 0)
                        {
                            paths.Remove(path.Key);

                        }

                    }

                }

                document["components"]?["securitySchemes"]?.AsObject().Remove("ServiceKey");

                return Results.Text(document.ToJsonString(), "application/json");
            }

            catch (HttpRequestException)
            {
                return Results.Problem("The documentation service is unavailable.", statusCode: 503);
            }

            catch (OperationCanceledException)when (!ct.IsCancellationRequested)
            {
                return Results.Problem("The documentation service timed out.", statusCode: 503);
            }

        }).AllowAnonymous().ExcludeFromDescription();

    }

}
