using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Application.History.Queries;
using WatchParty.Application.Identity.Queries;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.History;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Infrastructure.Persistence;
using WatchParty.Infrastructure.Persistence.Queries;
using WatchParty.Infrastructure.Persistence.Repositories;
using WatchParty.Infrastructure.Security;

namespace WatchParty.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(TimeProvider.System);

        AddSecurity(services, configuration);
        AddDatabase(services, configuration);
        AddPersistence(services);
        return services;
    }

    /// <summary>The readiness check: healthy only when Postgres answers.</summary>
    public static IHealthChecksBuilder AddDatabaseHealthCheck(this IHealthChecksBuilder healthChecks) =>
        healthChecks.AddDbContextCheck<WatchPartyDbContext>("database", tags: ["ready"]);

    /// <summary>
    /// Applies pending migrations when <see cref="DatabaseOptions.ApplyMigrationsOnStartup"/>
    /// is set (development and tests). Production runs <c>dotnet ef database update</c>, or a
    /// migration bundle, as its own deploy step instead.
    /// </summary>
    public static async Task MigrateDatabaseIfConfiguredAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var databaseOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!databaseOptions.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WatchPartyDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static void AddSecurity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<WatchPartyDbContext>((serviceProvider, dbContextOptions) =>
        {
            var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");

            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            dbContextOptions
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(databaseOptions.MaxRetryCount))
                .UseSnakeCaseNamingConvention();
        });
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IHistoryEntryRepository, HistoryEntryRepository>();

        services.AddScoped<IRoomQueries, RoomQueries>();
        services.AddScoped<IHistoryQueries, HistoryQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
    }
}
