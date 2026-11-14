using System.ComponentModel.DataAnnotations;

namespace WatchParty.Infrastructure.Persistence;

/// <summary>
/// The Postgres database. In production that is Neon: use its POOLED connection string
/// (the host containing <c>-pooler</c>) for the API, and the DIRECT one for migrations.
/// The connection string is a secret: user-secrets locally, an environment variable
/// (<c>ConnectionStrings__WatchParty</c>) or a secret store in production.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public const string ConnectionStringName = "WatchParty";

    /// <summary>
    /// Apply pending migrations when the API starts. For local development and tests
    /// only; production applies migrations as a reviewed deploy step (§2.5).
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Retries for transient failures, such as a Neon compute waking from suspend.</summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;
}
