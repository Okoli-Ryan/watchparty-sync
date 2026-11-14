using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WatchParty.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without starting the API. Adding a migration
/// needs no database; applying one reads the connection from
/// <c>WATCHPARTY_MIGRATIONS_CONNECTION</c> (use Neon's DIRECT, non-pooled, connection).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WatchPartyDbContext>
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=watchparty;Username=postgres;Password=postgres";

    public WatchPartyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WATCHPARTY_MIGRATIONS_CONNECTION")
            ?? LocalDevelopmentConnection;

        var options = new DbContextOptionsBuilder<WatchPartyDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WatchPartyDbContext(options);
    }
}
