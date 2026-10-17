using ECommerce.Catalog.Application.Products.Queries.GetProducts;
using ECommerce.Catalog.Infrastructure.Persistence;
using ECommerce.Catalog.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

internal static class ProductQueryChecks
{
    public static void Run()
    {
        using var db = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql("Host=localhost;Database=checks;Username=checks;Password=checks").Options);

        var queries = new ProductQueries(db);

        var id = Guid.NewGuid();

        var detailSql = queries.Project().Where(x => x.Id == id).ToQueryString();

        var filtered = queries.Filter(
            new GetProductsQuery { CategoryId = id, Search = @"100%_\", PageSize = 10, PageNumber = 2 });

        var pageSql = filtered.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(10).Take(10).ToQueryString();

        var countSql = filtered.GroupBy(_ => 1).Select(x => x.Count()).ToQueryString();

        if (!detailSql.Contains("JOIN") || !pageSql.Contains("ILIKE") || !pageSql.Contains("LIMIT") || !pageSql.Contains("OFFSET") || !countSql.Contains("count("))
        {
            throw new InvalidOperationException("Product queries must translate to PostgreSQL SQL.");

        }

        Console.WriteLine("Product PostgreSQL query translation checks passed.");

    }

}
