using ECommerce.Catalog.Domain.Aggregates.Categories;
using ECommerce.Catalog.Domain.Aggregates.Products;
using ECommerce.Catalog.Domain.Exceptions;
using ECommerce.Catalog.Domain.ValueObjects;
using System.Net;
using System.Net.Http.Json;

namespace ECommerce.Tests;

public sealed class CatalogTests
{
    [Fact]

    public Task Category_http_crud_and_conflict_checks() => CategoryApiChecks.RunAsync();

    [Fact]

    public Task Product_http_crud_filters_and_lifecycle_checks() => ProductApiChecks.RunAsync();

    [Fact]

    public Task Provider_failures_are_mapped_selectively() => CategoryPersistenceChecks.RunAsync();

    [Fact]

    public void PostgreSql_queries_translate() => ProductQueryChecks.Run();

    [Theory]

    [InlineData(-1.0)]

    [InlineData(1.001)]

    [InlineData(10000000000000000.0)]

    public void Money_rejects_unsupported_amounts(
        double value) => Assert.Throws<DomainException>(
        () => new Money((decimal)value, "USD"));

    [Theory]

    [InlineData("")]

    [InlineData("US")]

    [InlineData("123")]

    [InlineData("€UR")]

    [InlineData("USDD")]

    public void Money_rejects_invalid_currency(
        string currency) => Assert.Throws<DomainException>(
        () => new Money(1, currency));

    [Fact]

    public void Full_product_update_is_atomic_on_invalid_description()
    {
        var product = Product.Create("Original", "Original description", new Money(10, "USD"), Guid.NewGuid());

        Assert.Throws<DomainException>(
            () => product.UpdateDetails("Changed", new string ('x', 2001), new Money(5, "USD"), Guid.NewGuid()));

        Assert.Equal("Original", product.Name);

        Assert.Equal(10, product.Price.Amount);

    }

    [Fact]

    public void Category_update_is_atomic()
    {
        var category = Category.Create("Original", null);

        Assert.Throws<DomainException>(() => category.UpdateDetails("Changed", new string ('x', 1001)));

        Assert.Equal("Original", category.Name);

    }

    [Theory]

    [InlineData(false, HttpStatusCode.Forbidden)]

    [InlineData(true, HttpStatusCode.Created)]

    public async Task Writes_require_administrator(bool admin, HttpStatusCode expected)
    {
        await using var factory = new CategoryApiFactory();

        using var client = factory.CreateClient();

        TestCredentials.Authenticate(client, admin: admin);

        Assert.Equal(
            expected,
            (await client.PostAsJsonAsync("/api/categories", new { name = "Role check" })).StatusCode);

    }

    [Fact]

    public async Task Reads_are_public_but_anonymous_writes_are_rejected()
    {
        await using var factory = new CategoryApiFactory();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/categories")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/categories", new { name = "Forbidden" })).StatusCode);

    }

}
