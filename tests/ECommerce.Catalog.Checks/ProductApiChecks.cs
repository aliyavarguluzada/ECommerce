using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

internal static class ProductApiChecks
{
    public static async Task RunAsync()
    {
        await using var factory = new CategoryApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false });

        var electronics = (await (await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Electronics" })).Content.ReadFromJsonAsync<CategoryResponse>())!;

        var books = (await (await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Books" })).Content.ReadFromJsonAsync<CategoryResponse>())!;

        var created = await client.PostAsJsonAsync(
            "/api/products",
            new {
                name = " Mouse ",
                description = " Wireless ",
                price = 99.99m,
                currency = "usd",
                categoryId = electronics.Id });

        Require(created.StatusCode == HttpStatusCode.Created, "Create product returns 201");

        var mouse = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;

        Require(
            mouse.Name == "Mouse" && mouse.Description == "Wireless" && mouse.Currency == "USD" && mouse.CategoryName == "Electronics" && mouse.IsActive,
            "Created product includes normalized fields");

        var uri = $"/api/products/{mouse.Id}";

        Require(created.Headers.Location?.AbsolutePath == uri, "Product Location points to GET");

        Require((await client.GetFromJsonAsync<ProductResponse>(uri)) == mouse, "Product GET returns committed state");

        await Create(client, "100% Keyboard", electronics.Id);

        await Create(client, "Book", books.Id);

        var page = (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?pageSize=1&pageNumber=2"))!;

        Require(
            page.Items.Count == 1 && page.TotalCount == 3 && page.PageNumber == 2 && page.Items[0].Name == "Book",
            "Pagination returns total matching count and requested page");

        var filtered = (await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={electronics.Id}&search=mOuSe"))!;

        Require(
            filtered.TotalCount == 1 && filtered.Items.Single().Id == mouse.Id,
            "Category and case-insensitive search filters");

        var literal = (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?search=100%25"))!;

        Require(literal.TotalCount == 1 && literal.Items[0].Name == "100% Keyboard", "Search treats percent literally");

        Require(
            (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?pageNumber=100"))!.Items.Count == 0,
            "Page beyond results is empty");

        foreach (var query in new[]
        {
            "pageSize=0",

            "pageSize=101",

            "pageNumber=0",

            "pageNumber=2147483647&pageSize=100",

            "pageNumber=abc",

            "categoryId=00000000-0000-0000-0000-000000000000",

            "isActive=invalid"

        }

        )
        {
            await Problem(await client.GetAsync("/api/products?" + query), HttpStatusCode.BadRequest);

        }

        foreach (var input in new object[]
        {
            new
            {
                name = "Invalid",

                price = -1m,

                currency = "USD",

                categoryId = electronics.Id

            },

            new
            {
                name = "Invalid",

                price = 1.001m,

                currency = "USD",

                categoryId = electronics.Id

            },

            new
            {
                name = "Invalid",

                price = 10000000000000000m,

                currency = "USD",

                categoryId = electronics.Id

            },

            new
            {
                name = "",

                price = 1m,

                currency = "USD",

                categoryId = electronics.Id

            },

            new
            {
                name = "Invalid",

                price = 1m,

                currency = "12$",

                categoryId = electronics.Id

            },

            new
            {
                name = "Invalid",

                price = 1m,

                currency = "USD",

                categoryId = Guid.NewGuid()

            },

            new
            {
                name = "Invalid",

                currency = "USD",

                categoryId = electronics.Id

            },

            new
            {
                name = "Invalid",

                price = 1m,

                categoryId = electronics.Id

            }

        }

        )
        {
            await Problem(await client.PostAsJsonAsync("/api/products", input), HttpStatusCode.BadRequest);

        }

        Require(factory.Database.Products.Count == 3, "Invalid creations do not persist");

        Require(
            (await client.PutAsJsonAsync(
                uri,
                new {
                    name = " Updated mouse ",
                    description = " New ",
                    price = 90m,
                    currency = "USD",
                    categoryId = books.Id })).StatusCode == HttpStatusCode.NoContent,
            "Update product returns 204");

        var updated = (await client.GetFromJsonAsync<ProductResponse>(uri))!;

        Require(
            updated.Name == "Updated mouse" && updated.Price == 90m && updated.CategoryId == books.Id && updated.CreatedAt == mouse.CreatedAt,
            "Update changes details and preserves identity");

        await Problem(
            await client.PutAsJsonAsync(
                uri,
                new { name = "Rejected", price = 80m, currency = "EUR", categoryId = books.Id }),
            HttpStatusCode.BadRequest);

        await Problem(
            await client.PutAsJsonAsync(
                uri,
                new { name = "Rejected", price = 80m, currency = "USD", categoryId = Guid.NewGuid() }),
            HttpStatusCode.BadRequest);

        await Problem(
            await client.PutAsJsonAsync(
                uri,
                new {
                    name = "Rejected",
                    description = new string ('x', 2001),
                    price = 80m,
                    currency = "USD",
                    categoryId = books.Id }),
            HttpStatusCode.BadRequest);

        Require((await client.GetFromJsonAsync<ProductResponse>(uri)) == updated, "Failed updates are not persisted");

        Require(
            (await client.PatchAsJsonAsync(
                uri + "/price",
                new { price = 75.50m, currency = "USD" })).StatusCode == HttpStatusCode.NoContent,
            "Change price returns 204");

        var repriced = (await client.GetFromJsonAsync<ProductResponse>(uri))!;

        Require(
            repriced.Price == 75.50m && repriced.Name == updated.Name && repriced.CategoryId == books.Id,
            "Price change preserves details");

        await Problem(
            await client.PatchAsJsonAsync(uri + "/price", new { price = 1m, currency = "EUR" }),
            HttpStatusCode.BadRequest);

        await Problem(
            await client.PatchAsJsonAsync(uri + "/price", new { currency = "USD" }),
            HttpStatusCode.BadRequest);

        factory.Database.NextCommitFailure = new InvalidProductCategoryException();

        await Problem(
            await client.PutAsJsonAsync(
                uri,
                new { name = "Race", price = 10m, currency = "USD", categoryId = electronics.Id }),
            HttpStatusCode.BadRequest);

        factory.Database.NextCommitFailure = new ProductConcurrencyException();

        await Problem(
            await client.PatchAsJsonAsync(uri + "/price", new { price = 2m, currency = "USD" }),
            HttpStatusCode.Conflict);

        Require(
            (await client.GetFromJsonAsync<ProductResponse>(uri)) == repriced,
            "Commit failures preserve persisted data");

        Require((await client.DeleteAsync(uri)).StatusCode == HttpStatusCode.NoContent, "Discontinue returns 204");

        Require(
            (await client.DeleteAsync(uri)).StatusCode == HttpStatusCode.NoContent,
            "Discontinuation is idempotent");

        var discontinued = (await client.GetFromJsonAsync<ProductResponse>(uri))!;

        Require(!discontinued.IsActive && discontinued.Id == mouse.Id, "Discontinued product remains retrievable");

        Require(
            (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products"))!.TotalCount == 2,
            "Default listing excludes discontinued products");

        Require(
            (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?isActive=false"))!.Items.Single().Id == mouse.Id,
            "Inactive filter returns discontinued products");

        Require(
            (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?includeInactive=true"))!.TotalCount == 3,
            "IncludeInactive lists all products");

        await Problem(await client.DeleteAsync($"/api/categories/{books.Id}"), HttpStatusCode.Conflict);

        var missing = "/api/products/" + Guid.NewGuid();

        Require(
            (await client.GetAsync(missing)).StatusCode == HttpStatusCode.NotFound,
            "Missing product GET returns 404");

        Require(
            (await client.DeleteAsync(missing)).StatusCode == HttpStatusCode.NotFound,
            "Missing product DELETE returns 404");

        Require(
            (await client.PutAsJsonAsync(
                missing,
                new {
                    name = "Missing",
                    price = 1m,
                    currency = "USD",
                    categoryId = books.Id })).StatusCode == HttpStatusCode.NotFound,
            "Missing product update returns 404");

        Require(
            (await client.PatchAsJsonAsync(
                missing + "/price",
                new { price = 1m, currency = "USD" })).StatusCode == HttpStatusCode.NotFound,
            "Missing price target returns 404");

        Require(
            (await client.GetAsync("/health/live")).StatusCode == HttpStatusCode.OK,
            "Liveness is independent of PostgreSQL");

        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        using var devClient = development.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var openApi = await devClient.GetAsync("/openapi/v1.json");

        Require(
            openApi.StatusCode == HttpStatusCode.OK && (await openApi.Content.ReadAsStringAsync()).Contains("/api/products/{id}/price"),
            "OpenAPI contains product routes");

        Require(
            (await devClient.GetAsync("/swagger/index.html")).StatusCode == HttpStatusCode.OK,
            "Swagger UI is available in development");

        Console.WriteLine("Product HTTP workflow checks passed (service persistence test double).");

    }

    private static async Task Create(HttpClient client, string name, Guid categoryId)
    {
        Require(
            (await client.PostAsJsonAsync(
                "/api/products",
                new { name, price = 10m, currency = "USD", categoryId })).StatusCode == HttpStatusCode.Created,
            "Create query fixture product");

    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        Require(
            response.StatusCode == status,
            $"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        Require(
            response.Content.Headers.ContentType?.MediaType == "application/problem+json",
            "ProblemDetails content type");

        Require(
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status == (int)status,
            "ProblemDetails status");

    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

    }

}
