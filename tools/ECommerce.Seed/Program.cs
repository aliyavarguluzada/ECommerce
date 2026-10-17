using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Seed;

public static class Program
{
    private const string Dataset = "ecommerce-seed-v1";

    private static readonly string[] Services = ["Catalog", "Identity", "Order", "Payment", "Inventory", "Cart"];

    public static async Task Main()
    {
        var connection = Environment.GetEnvironmentVariable("SEED_POSTGRES_CONNECTION") ?? throw new InvalidOperationException("Set SEED_POSTGRES_CONNECTION to the local PostgreSQL maintenance database connection.");

        var builder = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = "postgres",

            IncludeErrorDetail = false

        };

        await using var admin = new NpgsqlConnection(builder.ConnectionString);

        await admin.OpenAsync();

        await using var runLock = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtextextended('ecommerce-seed-v1', 0))",
            admin);

        if (await runLock.ExecuteScalarAsync()is not true)
        {
            throw new InvalidOperationException("Another seed run is active.");

        }

        // Preflight every existing target before migrating or inserting any application data.

        foreach (var service in Services)
        {
            var database = DatabaseName(service);

            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM pg_database WHERE datname = @name)",
                admin);

            exists.Parameters.AddWithValue("name", database);

            if (await exists.ExecuteScalarAsync()is true)
            {
                builder.Database = database;

                await using var target = new NpgsqlConnection(builder.ConnectionString);

                await target.OpenAsync();

                await AssertOwnershipAsync(target);

            }

        }

        var results = new List<object>();

        foreach (var service in Services)
        {
            var timer = Stopwatch.StartNew();

            var database = DatabaseName(service);

            await using var exists = new NpgsqlCommand(
                "SELECT EXISTS(SELECT FROM pg_database WHERE datname = @name)",
                admin);

            exists.Parameters.AddWithValue("name", database);

            if (await exists.ExecuteScalarAsync()is false)
            {
                // Database names are fixed constants, never caller input.

                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);

                await create.ExecuteNonQueryAsync();

            }

            builder.Database = database;

            await using var target = new NpgsqlConnection(builder.ConnectionString);

            await target.OpenAsync();

            await AssertOwnershipAsync(target);

            await ExecuteAsync(
                target,
                "CREATE TABLE IF NOT EXISTS seed_manifest (dataset text PRIMARY KEY, completed boolean NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), completed_at timestamptz); INSERT INTO seed_manifest(dataset, completed) VALUES ('ecommerce-seed-v1', false) ON CONFLICT DO NOTHING;");

            await using var context = Context(service, builder.ConnectionString);

            await context.Database.MigrateAsync();

            await using var completed = new NpgsqlCommand(
                "SELECT completed FROM seed_manifest WHERE dataset = @dataset",
                target);

            completed.Parameters.AddWithValue("dataset", Dataset);

            var alreadySeeded = await completed.ExecuteScalarAsync()is true;

            Console.WriteLine($"{database}: {(alreadySeeded ? "verifying existing dataset" : "bulk loading")}");

            // Temp fixtures also serve as independently generated expected relationships during verification.

            await ExecuteAsync(target, await SqlAsync("Common"));

            if (!alreadySeeded)
            {
                await using var transaction = await target.BeginTransactionAsync();

                await ExecuteAsync(target, await SqlAsync(service), transaction);

                if (service == "Identity")
                {
                    await SetDemoPasswordsAsync(target, transaction);

                }

                await VerifyAsync(target, service);

                await ExecuteAsync(
                    target,
                    "UPDATE seed_manifest SET completed = true, completed_at = now() WHERE dataset = 'ecommerce-seed-v1';",
                    transaction);

                await transaction.CommitAsync();

            }

            await VerifyAsync(target, service);

            await VerifyMaterializationAsync(context, service);

            await ExecuteAsync(
                target,
                "DROP TABLE seed_fingerprints, seed_addresses, seed_totals, seed_lines, seed_orders, seed_products;");

            await ExecuteAsync(target, "ANALYZE;");

            var tables = await TableCountsAsync(target);

            await using var size = new NpgsqlCommand("SELECT pg_database_size(current_database())", target);

            var bytes = (long)(await size.ExecuteScalarAsync())!;

            var benchmark = BenchmarkSql(service);

            await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + benchmark, target);

            using var plan = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);

            var queryMilliseconds = plan.RootElement[0].GetProperty("Execution Time").GetDouble();

            timer.Stop();

            results.Add(
                new {
                    Database = database,
                    Tables = tables,
                    Bytes = bytes,
                    ElapsedSeconds = timer.Elapsed.TotalSeconds,
                    SampleQuery = benchmark,
                    SampleQueryMilliseconds = queryMilliseconds,
                    Verification = "passed" });

            Console.WriteLine(
                $"  {tables.Values.Sum():N0} rows; {bytes / 1048576.0:N1} MiB; sample query {queryMilliseconds:N3} ms; integrity passed");

        }

        var output = Path.GetFullPath("artifacts/seed-report.json");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        await File.WriteAllTextAsync(
            output,
            JsonSerializer.Serialize(
                new { Dataset, VerifiedAtUtc = DateTime.UtcNow, Results = results },
                new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"Report: {output}");

        Console.WriteLine("Existing service databases and running APIs were not reconfigured.");

    }

    private static string DatabaseName(string service) => "ecommerce_seed_" + service.ToLowerInvariant();

    private static DbContext Context(string service, string connection) => service switch
    {
        "Catalog" => new ECommerce.Catalog.Infrastructure.Persistence.CatalogDbContext(
            new DbContextOptionsBuilder<ECommerce.Catalog.Infrastructure.Persistence.CatalogDbContext>().UseNpgsql(connection).Options),

        "Identity" => new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options),

        "Order" => new ECommerce.Order.Infrastructure.OrderDbContext(
            new DbContextOptionsBuilder<ECommerce.Order.Infrastructure.OrderDbContext>().UseNpgsql(connection).Options),

        "Payment" => new ECommerce.Payment.Infrastructure.PaymentDbContext(
            new DbContextOptionsBuilder<ECommerce.Payment.Infrastructure.PaymentDbContext>().UseNpgsql(connection).Options),

        "Inventory" => new ECommerce.Inventory.Infrastructure.InventoryDbContext(
            new DbContextOptionsBuilder<ECommerce.Inventory.Infrastructure.InventoryDbContext>().UseNpgsql(connection).Options),

        "Cart" => new ECommerce.Cart.Infrastructure.CartDbContext(
            new DbContextOptionsBuilder<ECommerce.Cart.Infrastructure.CartDbContext>().UseNpgsql(connection).Options),

        _ => throw new ArgumentException("Unknown service.", nameof(service))};

    private static Task<string> SqlAsync(
        string name) => File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Sql", name + ".sql"));

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 600

        };

        await command.ExecuteNonQueryAsync();

    }

    private static async Task AssertOwnershipAsync(NpgsqlConnection connection)
    {
        await using var tables = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'",
            connection);

        if ((long)(await tables.ExecuteScalarAsync())! == 0)
        {
            return;
        }

        await using var marker = new NpgsqlCommand(
            "SELECT to_regclass('public.seed_manifest') IS NOT NULL",
            connection);

        if (await marker.ExecuteScalarAsync()is not true)
        {
            throw new InvalidOperationException(
                $"Refusing an existing nonempty database without a seed manifest: {connection.Database}");

        }

        await using var dataset = new NpgsqlCommand(
            "SELECT count(*) FROM seed_manifest WHERE dataset = 'ecommerce-seed-v1'",
            connection);

        if ((long)(await dataset.ExecuteScalarAsync())! != 1)
        {
            throw new InvalidOperationException("The seed database belongs to another dataset version.");

        }

    }

    private static async Task SetDemoPasswordsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var password = Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD") ?? "Seed-" + Convert.ToHexString(
            RandomNumberGenerator.GetBytes(16)) + "!";

        var hasher = new PasswordHasher<ApplicationUser>();

        for (var index = 1; index <= 10; index++)
        {
            var user = new ApplicationUser
            {
                Email = $"user{index}@example.test"};

            var hash = hasher.HashPassword(user, password);

            await using var update = new NpgsqlCommand(
                "UPDATE \"AspNetUsers\" SET \"PasswordHash\" = @hash WHERE \"Email\" = @email",
                connection,
                transaction);

            update.Parameters.AddWithValue("hash", hash);

            update.Parameters.AddWithValue("email", user.Email);

            await update.ExecuteNonQueryAsync();

            if (hasher.VerifyHashedPassword(user, hash, password) == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("Demo password verification failed.");

            }

        }

        // .tools is ignored by Git. Never include generated credentials in reports or console output.

        Directory.CreateDirectory(".tools");

        await File.WriteAllTextAsync(
            ".tools/seed-demo-login.txt",
            $"SEED DATABASES ONLY\nAdmin: user1@example.test\nCustomers: user2@example.test through user10@example.test\nPassword: {password}\nOther users have no password hash.\n");

    }

    private static async Task<Dictionary<string, long>> TableCountsAsync(NpgsqlConnection connection)
    {
        var names = new List<string>();

        await using (var command = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename",
            connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));

            }

        }

        var counts = new Dictionary<string, long>();

        foreach (var name in names)
        {
            var quoted = '"' + name.Replace("\"", "\"\"") + '"';

            await using var command = new NpgsqlCommand($"SELECT count(*) FROM {quoted}", connection);

            counts[name] = (long)(await command.ExecuteScalarAsync())!;

        }

        return counts;
    }

    private static async Task VerifyAsync(NpgsqlConnection connection, string service)
    {
        var expected = service switch
        {
            "Catalog" => new Dictionary<string, long>
            {
                ["categories"] = 50,

                ["products"] = 1000

            },

            "Identity" => new Dictionary<string, long>
            {
                ["AspNetUsers"] = 100000,

                ["AspNetRoles"] = 2,

                ["AspNetUserRoles"] = 100001,

                ["AspNetRoleClaims"] = 2,

                ["AspNetUserClaims"] = 100000,

                ["AspNetUserLogins"] = 1000,

                ["AspNetUserTokens"] = 1000,

                ["refresh_sessions"] = 1000

            },

            "Order" => new Dictionary<string, long>
            {
                ["orders"] = 100000,

                ["order_items"] = 300000

            },

            "Payment" => new Dictionary<string, long>
            {
                ["payments"] = 95000

            },

            "Inventory" => new Dictionary<string, long>
            {
                ["stock_items"] = 1000,

                ["stock_adjustments"] = 1000,

                ["reservations"] = 100000,

                ["reservation_items"] = 300000

            },

            "Cart" => new Dictionary<string, long>
            {
                ["carts"] = 20000,

                ["cart_items"] = 60000,

                ["checkout_attempts"] = 100000,

                ["checkout_items"] = 300000

            },

            _ => throw new InvalidOperationException("Unknown service.")};

        var counts = await TableCountsAsync(connection);

        foreach (var(table, count)in expected)
        {
            if (counts.GetValueOrDefault(table, -1) != count)
            {
                throw new InvalidOperationException($"Unexpected row count: {connection.Database}.{table}");

            }

        }

        await ExecuteAsync(connection, await SqlAsync(service + ".Verify"));

    }

    private static async Task VerifyMaterializationAsync(DbContext context, string service)
    {
        switch (service)
        {
            case "Catalog":

                var products = await ((ECommerce.Catalog.Infrastructure.Persistence.CatalogDbContext)context).Products.AsNoTracking().Take(10).ToListAsync();

                if (products.Any(product => product.Price.Currency != "USD" || product.Price.Amount <= 0))
                {
                    throw new InvalidOperationException("Catalog entity materialization failed.");

                }

                break;

            case "Identity":

                var users = await ((IdentityDbContext)context).Users.AsNoTracking().Where(
                    user => user.Email == "user1@example.test").ToListAsync();

                if (users.Count != 1 || string.IsNullOrWhiteSpace(users[0].PasswordHash))
                {
                    throw new InvalidOperationException("Seed admin materialization failed.");

                }

                break;

            case "Order":

                var orders = await ((ECommerce.Order.Infrastructure.OrderDbContext)context).Orders.AsNoTracking().OrderBy(
                    order => order.Id).Take(25).ToListAsync();

                foreach (var order in orders)
                {
                    var items = order.Items.OrderBy(
                        item => item.ProductId).Select(
                        item => new ECommerce.Order.Application.OrderItemRequest(
                            item.ProductId,
                            item.Quantity)).ToArray();

                    var json = JsonSerializer.Serialize(
                        new { order.CustomerId, Address = order.ShippingAddress, Items = items });

                    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

                    if (order.RequestFingerprint != fingerprint || order.Total != order.Items.Sum(
                        item => item.LineTotal))
                    {
                        throw new InvalidOperationException("Order replay fingerprint or domain total mismatch.");

                    }

                }

                break;

            case "Cart":

                var checkouts = await ((ECommerce.Cart.Infrastructure.CartDbContext)context).Checkouts.AsNoTracking().Take(25).ToListAsync();

                foreach (var checkout in checkouts)
                {
                    var address = JsonSerializer.Deserialize<ECommerce.Cart.Application.AddressRequest>(
                        checkout.AddressJson) ?? throw new InvalidOperationException("Invalid seed address.");

                    var hash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(address.Normalize()))));

                    if (hash != checkout.AddressHash || checkout.Items.Count != 3)
                    {
                        throw new InvalidOperationException("Checkout snapshot/hash materialization failed.");

                    }

                }

                break;

            case "Inventory":

                var reservations = await ((ECommerce.Inventory.Infrastructure.InventoryDbContext)context).Reservations.AsNoTracking().Take(10).ToListAsync();

                if (reservations.Any(
                    reservation => reservation.Items.Count != 3 || reservation.Status == ECommerce.Inventory.Domain.ReservationStatus.Reserved))
                {
                    throw new InvalidOperationException("Reservation entity materialization failed.");

                }

                break;

            case "Payment":

                var payments = await ((ECommerce.Payment.Infrastructure.PaymentDbContext)context).Payments.AsNoTracking().Take(10).ToListAsync();

                if (payments.Any(payment => !payment.OrderNotified || payment.Amount <= 0))
                {
                    throw new InvalidOperationException("Payment entity materialization failed.");

                }

                break;

        }

    }

    private static string BenchmarkSql(string service) => service switch
    {
        "Catalog" => "SELECT * FROM products WHERE \"IsActive\" ORDER BY \"Name\" LIMIT 20",

        "Identity" => "SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = 'USER50000@EXAMPLE.TEST'",

        "Order" => "SELECT \"Id\", \"Total\" FROM orders WHERE \"CustomerId\" = pg_temp.seed_id('user', 50001) ORDER BY \"CreatedAt\" DESC LIMIT 20",

        "Payment" => "SELECT \"Id\", \"Amount\" FROM payments WHERE \"CustomerId\" = pg_temp.seed_id('user', 50001) ORDER BY \"CreatedAt\" DESC LIMIT 20",

        "Inventory" => "SELECT * FROM stock_items WHERE \"ProductId\" = pg_temp.seed_id('product', 501)",

        "Cart" => "SELECT * FROM cart_items WHERE \"CustomerId\" = pg_temp.seed_id('user', 10001)",

        _ => throw new InvalidOperationException("Unknown service.")};

}
