using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Common;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class PostgreSqlIntegrationChecks
{
    public static async Task RunAsync()
    {
        var configured = Environment.GetEnvironmentVariable("CATALOG_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            Console.WriteLine("Live PostgreSQL checks skipped: set CATALOG_TEST_CONNECTION_STRING to enable.");
            return;
        }

        // All objects created by this check belong to its uniquely named schema.
        var schema = "catalog_checks_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(configured);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(configured) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options;
            await using (var db = new CatalogDbContext(options)) await db.Database.MigrateAsync();
            await using var factory = new LiveCatalogFactory(connection);
            using (var scope = factory.Services.CreateScope())
            {
                var actual = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.GetConnectionString();
                Ensure(new NpgsqlConnectionStringBuilder(actual).SearchPath == schema,
                    "Integration HTTP host must use the isolated test schema");
            }
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

            var simultaneous = await Task.WhenAll(
                client.PostAsJsonAsync("/api/categories", new { name = "Electronics" }),
                client.PostAsJsonAsync("/api/categories", new { name = "Electronics" }));
            Ensure(simultaneous.Count(x => x.StatusCode == HttpStatusCode.Created) == 1 &&
                simultaneous.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1, "Database enforces duplicate category uniqueness");
            var category = (await simultaneous.Single(x => x.StatusCode == HttpStatusCode.Created)
                .Content.ReadFromJsonAsync<CategoryResponse>())!;
            await CheckCategoriesAsync(client, options, category);
            var created = await client.PostAsJsonAsync("/api/products",
                new { name = "100% Mouse", price = 99.99m, currency = "USD", categoryId = category.Id });
            Ensure(created.StatusCode == HttpStatusCode.Created, "PostgreSQL product insert");
            var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
            var uri = $"/api/products/{product.Id}";
            Ensure((await client.PatchAsJsonAsync(uri + "/price", new { price = 70.50m, currency = "USD" }))
                .StatusCode == HttpStatusCode.NoContent, "Owned Money replacement persists");
            Ensure((await client.GetFromJsonAsync<ProductResponse>(uri))!.Price == 70.50m, "Price round trip");
            var results = (await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?search=100%25"))!;
            Ensure(results.TotalCount == 1, "Real PostgreSQL ILIKE escapes literal percent");
            Ensure((await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?search=_"))!.TotalCount == 0,
                "Real PostgreSQL ILIKE escapes literal underscore");

            await using (var first = new CatalogDbContext(options))
            await using (var second = new CatalogDbContext(options))
            {
                var one = await first.Products.SingleAsync(x => x.Id == product.Id);
                var two = await second.Products.SingleAsync(x => x.Id == product.Id);
                Ensure(one.DomainEvents.Count == 0, "Domain events are not materialized from the database");
                one.Rename("First edit");
                await first.SaveChangesAsync();
                two.Rename("Competing edit");
                var rejected = false;
                try { await second.SaveChangesAsync(); }
                catch (ProductConcurrencyException) { rejected = true; }
                Ensure(rejected, "Concurrent product save is rejected");
            }

            Ensure((await client.DeleteAsync(uri)).StatusCode == HttpStatusCode.NoContent, "PostgreSQL discontinuation");
            Ensure((await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products"))!.TotalCount == 0,
                "Discontinued product hidden by default");
            Ensure((await client.GetFromJsonAsync<ProductResponse>(uri))!.IsActive == false, "Discontinued row retained");
            Ensure((await client.DeleteAsync($"/api/categories/{category.Id}")).StatusCode == HttpStatusCode.Conflict,
                "Discontinued product still protects its category");
            Ensure((await client.GetAsync("/health/ready")).StatusCode == HttpStatusCode.OK, "Migrated database is ready");
            Console.WriteLine("Live PostgreSQL migration, HTTP, Money, search, and concurrency checks passed.");
        }
        finally
        {
            // The schema name is generated above and cannot identify user-owned tables.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static void Ensure(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }

    private static async Task CheckCategoriesAsync(HttpClient client,
        DbContextOptions<CatalogDbContext> options, CategoryResponse category)
    {
        var uri = $"/api/categories/{category.Id}";
        var fetched = (await client.GetFromJsonAsync<CategoryResponse>(uri))!;
        Ensure(fetched.Id == category.Id && fetched.Name == category.Name &&
            fetched.CreatedAt.Kind == DateTimeKind.Utc, "Category PostgreSQL retrieval and UTC timestamps");
        var createBooks = await client.PostAsJsonAsync("/api/categories", new { name = "Books", description = "Reading" });
        Ensure(createBooks.StatusCode == HttpStatusCode.Created, "Create second category");
        var books = (await createBooks.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var listed = (await client.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!;
        Ensure(listed.Select(x => x.Name).SequenceEqual(new[] { "Books", "Electronics" }), "PostgreSQL category ordering");

        Ensure((await client.PutAsJsonAsync(uri, new { name = " Electronics ", description = " Updated " }))
            .StatusCode == HttpStatusCode.NoContent, "Category PostgreSQL update");
        var updated = (await client.GetFromJsonAsync<CategoryResponse>(uri))!;
        Ensure(updated.Name == "Electronics" && updated.Description == "Updated" && updated.CreatedAt == fetched.CreatedAt &&
            updated.UpdatedAt >= fetched.UpdatedAt, "Category fields and timestamps persist");
        Ensure((await client.PutAsJsonAsync(uri, new { name = "Books" })).StatusCode == HttpStatusCode.Conflict,
            "Duplicate rename returns 409");
        Ensure((await client.PutAsJsonAsync(uri, new { name = "Rejected", description = new string('x', 1001) }))
            .StatusCode == HttpStatusCode.BadRequest, "Invalid category update returns 400");
        Ensure((await client.GetFromJsonAsync<CategoryResponse>(uri)) == updated, "Failed category updates do not persist");
        Ensure((await client.PutAsJsonAsync(uri, new { name = "Electronics", description = " " }))
            .StatusCode == HttpStatusCode.NoContent, "Clear category description");
        Ensure((await client.GetFromJsonAsync<CategoryResponse>(uri))!.Description is null, "Cleared description persists as null");

        await using (var first = new CatalogDbContext(options))
        await using (var second = new CatalogDbContext(options))
        {
            var one = await first.Categories.SingleAsync(x => x.Id == category.Id);
            var two = await second.Categories.SingleAsync(x => x.Id == category.Id);
            one.UpdateDescription("First edit");
            await first.SaveChangesAsync();
            two.UpdateDescription("Competing edit");
            var rejected = false;
            try { await second.SaveChangesAsync(); }
            catch (CategoryConcurrencyException) { rejected = true; }
            Ensure(rejected, "Concurrent category update is rejected by PostgreSQL");
        }

        Ensure((await client.DeleteAsync($"/api/categories/{books.Id}")).StatusCode == HttpStatusCode.NoContent,
            "Delete unused category in PostgreSQL");
        Ensure((await client.GetAsync($"/api/categories/{books.Id}")).StatusCode == HttpStatusCode.NotFound,
            "Deleted category is absent");
        Ensure((await client.DeleteAsync($"/api/categories/{books.Id}")).StatusCode == HttpStatusCode.NotFound,
            "Repeated category deletion returns 404");
        Ensure((await client.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!.Length == 1,
            "List excludes deleted categories");
    }

    private sealed class LiveCatalogFactory(string connection) : WebApplicationFactory<ECommerce.Catalog.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/ECommerce.Catalog.Api")));
            builder.UseSetting("ConnectionStrings:CatalogDatabase", connection);
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }
}
