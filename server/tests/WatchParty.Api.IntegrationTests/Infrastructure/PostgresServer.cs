using Npgsql;
using Testcontainers.PostgreSql;

namespace WatchParty.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One throwaway Postgres for the whole test run (§10: a real database, never a mock).
/// The same major version Neon runs. Testcontainers removes the container when the
/// test process exits.
/// </summary>
internal static class PostgresServer
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    /// <summary>A connection string to a new, empty database of its own on the shared server.</summary>
    public static async Task<string> CreateDatabaseAsync()
    {
        var container = await Container.Value;
        var databaseName = $"watchparty_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The name is a generated GUID, never user input; CREATE DATABASE takes no parameters.
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = databaseName };
        return builder.ConnectionString;
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder("postgres:17-alpine").Build();

        await container.StartAsync();
        return container;
    }
}
