using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Tests.Support;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresDatabase.Variable)))
        {
            Skip = $"Set {PostgresDatabase.Variable} to run real PostgreSQL persistence tests.";

        }

    }

}

// Each test owns only a new random schema. Never migrate or delete public tables.

public sealed class PostgresDatabase : IAsyncDisposable
{
    public const string Variable = "ECOMMERCE_TEST_POSTGRES_CONNECTION";

    private readonly string _adminConnection;

    private readonly string _schema = "ecommerce_tests_" + Guid.NewGuid().ToString("N");

    public string Connection { get; }

    private PostgresDatabase(string connection)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection)
        {
            Pooling = false

        };

        _adminConnection = builder.ConnectionString;

        builder.SearchPath = _schema;

        Connection = builder.ConnectionString;

    }

    public static async Task<PostgresDatabase> CreateAsync()
    {
        var database = new PostgresDatabase(
            Environment.GetEnvironmentVariable(Variable) ?? throw new InvalidOperationException(
                $"{Variable} is required."));

        await using var connection = new NpgsqlConnection(database._adminConnection);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{database._schema}\"", connection);

        await command.ExecuteNonQueryAsync();

        return database;
    }

    public DbContextOptions<T> Options<T>()

        where T : DbContext => new DbContextOptionsBuilder<T>().UseNpgsql(Connection).Options;

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnection);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);

        await command.ExecuteNonQueryAsync();

    }

}
