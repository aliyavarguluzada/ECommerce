using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application;
using ECommerce.Catalog.Infrastructure;
using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

internal static class CategoryApiChecks
{
    public static async Task RunAsync()
    {
        await using var factory = new CategoryApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false });

        var created = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = " Electronics ", description = " Devices " });

        Require(created.StatusCode == HttpStatusCode.Created, "Create returns 201");

        var category = (await created.Content.ReadFromJsonAsync<CategoryResponse>())!;

        Require(
            category.Id != Guid.Empty && category.Name == "Electronics" && category.Description == "Devices",
            "Response contains normalized domain data");

        Require(factory.Database.Categories.Count == 1, "Create commits the category");

        Require(category.UpdatedAt == category.CreatedAt, "New category exposes matching UTC timestamps");

        Require(created.Headers.Location?.AbsolutePath == $"/api/categories/{category.Id}", "Location points to GET");

        var fetched = await client.GetFromJsonAsync<CategoryResponse>(created.Headers.Location);

        Require(fetched == category, "GET returns committed category");

        Require(
            (await client.GetAsync($"/api/categories/{Guid.NewGuid()}")).StatusCode == HttpStatusCode.NotFound,
            "Missing category returns 404");

        var duplicate = await client.PostAsJsonAsync("/api/categories", new { name = " Electronics " });

        await RequireProblem(duplicate, HttpStatusCode.Conflict);

        Require(factory.Database.Categories.Count == 1, "Duplicate doesn't commit");

        foreach (var request in new object[]
        {
            new
            {
                name = " "

            },

            new
            {
                name = new string ('x', 151)

            },

            new
            {
                name = "Books",

                description = new string ('x', 1001)

            },

            new
            {
                description = "Missing name"

            }

        }

        )
        {
            await RequireProblem(await client.PostAsJsonAsync("/api/categories", request), HttpStatusCode.BadRequest);

        }

        Require(factory.Database.Categories.Count == 1, "Validation failures don't commit");

        // Simulate a concurrent request winning the unique-index race at commit.

        factory.Database.RejectNextCommit = true;

        await RequireProblem(
            await client.PostAsJsonAsync("/api/categories", new { name = "Books" }),
            HttpStatusCode.Conflict);

        Require(factory.Database.Categories.Count == 1, "Commit conflict doesn't report success");

        await CheckRemainingOperations(client, factory, category);

        Console.WriteLine("Category HTTP workflow checks passed (service persistence test double).");

    }

    private static async Task CheckRemainingOperations(
        HttpClient client,
        CategoryApiFactory factory,
        CategoryResponse category)
    {
        var booksResponse = await client.PostAsJsonAsync("/api/categories", new { name = "Books" });

        var books = (await booksResponse.Content.ReadFromJsonAsync<CategoryResponse>())!;

        var categories = (await client.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!;

        Require(
            categories.Select(x => x.Name).SequenceEqual(new[] { "Books", "Electronics" }),
            "List is sorted by name");

        var uri = $"/api/categories/{category.Id}";

        var update = await client.PutAsJsonAsync(uri, new { name = " Electronics ", description = " Updated " });

        Require(update.StatusCode == HttpStatusCode.NoContent, "Updating own name returns 204");

        var updated = (await client.GetFromJsonAsync<CategoryResponse>(uri))!;

        Require(
            updated.Name == "Electronics" && updated.Description == "Updated" && updated.CreatedAt == category.CreatedAt,
            "Update normalizes fields and preserves identity/creation time");

        Require(
            updated.UpdatedAt >= category.UpdatedAt && updated.UpdatedAt.Kind == DateTimeKind.Utc,
            "Update exposes its UTC modification timestamp");

        await RequireProblem(await client.PutAsJsonAsync(uri, new { name = " Books " }), HttpStatusCode.Conflict);

        await RequireProblem(
            await client.PutAsJsonAsync(uri, new { name = "Changed", description = new string ('x', 1001) }),
            HttpStatusCode.BadRequest);

        await RequireProblem(await client.PutAsJsonAsync(uri, new { name = " " }), HttpStatusCode.BadRequest);

        factory.Database.RejectNextCommit = true;

        await RequireProblem(await client.PutAsJsonAsync(uri, new { name = "Changed" }), HttpStatusCode.Conflict);

        Require(
            (await client.GetFromJsonAsync<CategoryResponse>(uri)) == updated,
            "Failed updates leave persisted values intact");

        var missing = $"/api/categories/{Guid.NewGuid()}";

        Require(
            (await client.PutAsJsonAsync(missing, new { name = "Missing" })).StatusCode == HttpStatusCode.NotFound,
            "Updating missing category returns 404");

        Require(
            (await client.DeleteAsync(missing)).StatusCode == HttpStatusCode.NotFound,
            "Deleting missing category returns 404");

        factory.Database.CategoriesWithProducts.Add(category.Id);

        await RequireProblem(await client.DeleteAsync(uri), HttpStatusCode.Conflict);

        Require((await client.GetAsync(uri)).StatusCode == HttpStatusCode.OK, "In-use category remains persisted");

        factory.Database.CategoriesWithProducts.Clear();

        factory.Database.NextCommitFailure = new CategoryInUseException();

        await RequireProblem(await client.DeleteAsync(uri), HttpStatusCode.Conflict);

        Require(
            (await client.GetAsync(uri)).StatusCode == HttpStatusCode.OK,
            "Foreign-key race does not delete category");

        factory.Database.NextCommitFailure = new CategoryConcurrencyException();

        await RequireProblem(await client.PutAsJsonAsync(uri, new { name = "Changed" }), HttpStatusCode.Conflict);

        Require(
            (await client.GetFromJsonAsync<CategoryResponse>(uri)) == updated,
            "Concurrent failure does not persist update");

        Require((await client.DeleteAsync(uri)).StatusCode == HttpStatusCode.NoContent, "Delete returns 204");

        Require((await client.GetAsync(uri)).StatusCode == HttpStatusCode.NotFound, "Deleted category is absent");

        Require((await client.DeleteAsync(uri)).StatusCode == HttpStatusCode.NotFound, "Repeated delete returns 404");

        Require(
            (await client.DeleteAsync($"/api/categories/{books.Id}")).StatusCode == HttpStatusCode.NoContent,
            "Delete remaining category");

        Require(
            (await client.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!.Length == 0,
            "Empty list returns an array");

        var descriptionTest = (await (await client.PostAsJsonAsync(
            "/api/categories",
            new {
                name = "Clear description",
                description = "Original" })).Content.ReadFromJsonAsync<CategoryResponse>())!;

        var descriptionUri = $"/api/categories/{descriptionTest.Id}";

        Require(
            (await client.PutAsJsonAsync(
                descriptionUri,
                new { name = descriptionTest.Name, description = " " })).StatusCode == HttpStatusCode.NoContent,
            "Blank description may clear an existing value");

        Require(
            (await client.GetFromJsonAsync<CategoryResponse>(descriptionUri))!.Description is null,
            "Cleared description is returned as null");

    }

    private static async Task RequireProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Require(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}");

        Require(
            response.Content.Headers.ContentType?.MediaType == "application/problem+json",
            "ProblemDetails content type");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Require(problem?.Status == (int)status, "ProblemDetails status");

    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);

        }

    }

}

internal sealed class CategoryApiFactory : WebApplicationFactory<ECommerce.Catalog.Api.Program>
{
    public CategoryApiFactory() => ECommerce.Testing.TestCredentials.Initialize();

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        ECommerce.Testing.TestCredentials.Authenticate(client, admin: true);

    }

    public FakeCategoryDatabase Database { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.UseContentRoot(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/ECommerce.Catalog.Api")));

        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICatalogService>();
            services.AddSingleton(Database);
            services.AddScoped<ICatalogService, FakeCategorySession>();

        });

    }

}

internal sealed class FakeCategoryDatabase
{
    public Dictionary<Guid, Category> Categories { get; } = [];

    public bool RejectNextCommit { get; set; }

    public HashSet<Guid> CategoriesWithProducts { get; } = [];

    public Exception? NextCommitFailure { get; set; }

    public Dictionary<Guid, Product> Products { get; } = [];

}

internal sealed class FakeCategorySession(
    FakeCategoryDatabase database) : CatalogService(null!)
{
    private Category? _pending;

    private bool _delete;

    private Product? _pendingProduct;

    protected override Task<bool> CategoryNameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default) => Task.FromResult(
        database.Categories.Values.Any(x => x.Name == name && x.Id != excludingId));

    protected override Task<IReadOnlyList<Category>> ReadCategoriesAsync(
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(
        database.Categories.Values.OrderBy(x => x.Name).ThenBy(x => x.Id).ToArray());

    protected override Task<Category?> FindCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = database.Categories.GetValueOrDefault(id);

        // Simulate EF's per-request entity instance, isolated from committed storage.

        _pending = category is null ? null : (Category)typeof(object).GetMethod(
            "MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(
            category,
            null)!;

        return Task.FromResult(_pending);
    }

    protected override Task<bool> CategoryHasProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) => Task.FromResult(
        database.CategoriesWithProducts.Contains(id) || database.Products.Values.Any(x => x.CategoryId == id));

    protected override void RemoveCategory(Category category)
    {
        _pending = category;

        _delete = true;

    }

    protected override Task<Category?> FindCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default) => Task.FromResult(
        database.Categories.GetValueOrDefault(id));

    protected override Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default)
    {
        _pending = category;

        return Task.CompletedTask;
    }

    protected override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (database.NextCommitFailure is { } failure)
        {
            database.NextCommitFailure = null;

            throw failure;

        }

        if (database.RejectNextCommit)
        {
            database.RejectNextCommit = false;

            throw new DuplicateCategoryException();

        }

        if (_pendingProduct is not null)
        {
            database.Products[_pendingProduct.Id] = _pendingProduct;

        }

        else if (_delete)
        {
            database.Categories.Remove(_pending!.Id);

        }

        else if (_pending is not null)
        {
            database.Categories[_pending.Id] = _pending;

        }

        _pending = null;

        return Task.FromResult(1);
    }

    protected override Task<Product?> FindProductForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = database.Products.GetValueOrDefault(id);

        _pendingProduct = product is null ? null : (Product)typeof(object).GetMethod(
            "MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(
            product,
            null)!;

        return Task.FromResult(_pendingProduct);
    }

    protected override Task AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        _pendingProduct = product;

        return Task.CompletedTask;
    }

    protected override Task<ProductResponse?> ReadProductAsync(
        Guid id,
        CancellationToken cancellationToken) => Task.FromResult(
        database.Products.TryGetValue(id, out var product) ? Respond(product) : null);

    protected override Task<PagedResult<ProductResponse>> ReadProductsAsync(
        ProductFilter query,
        CancellationToken cancellationToken = default)
    {
        query.Validate();

        var products = database.Products.Values.AsEnumerable();

        var activeFilter = query.IsActive ?? (query.IncludeInactive ? (bool? )null : true);

        if (activeFilter is { } active)
        {
            products = products.Where(x => x.IsActive == active);

        }

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(x => x.CategoryId == categoryId);

        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            products = products.Where(x => x.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        }

        var ordered = products.OrderBy(x => x.Name).ThenBy(x => x.Id).ToArray();

        return Task.FromResult(
            new PagedResult<ProductResponse>(
                ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(Respond).ToArray(),
                ordered.Length,
                query.PageNumber,
                query.PageSize));
    }

    private ProductResponse Respond(
        Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price.Amount,
        product.Price.Currency,
        product.CategoryId,
        database.Categories[product.CategoryId].Name,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);

}
