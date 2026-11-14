using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence;

/// <summary>
/// The <see cref="WatchPartyDbContext"/> is the unit of work (§2.3): repositories only
/// stage changes, this commits them in one transaction and then dispatches the domain
/// events the changed aggregates raised.
/// </summary>
internal sealed partial class UnitOfWork(
    WatchPartyDbContext dbContext,
    DomainEventDispatcher dispatcher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        // Collected before saving: entities deleted by this commit are no longer
        // tracked afterwards, but their events (a member leaving) still need sending.
        var aggregates = dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .ToList();

        IncrementVersionsOfModifiedAggregates();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            Discard(aggregates);
            return ConcurrencyErrors.Conflict;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            LogUniqueViolation(exception.InnerException!.Message);
            Discard(aggregates);
            return ConcurrencyErrors.Duplicate;
        }

        var domainEvents = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await dispatcher.DispatchAsync(domainEvent, cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// The new version is what lands in the row; the version the aggregate was loaded at
    /// stays the original value, which EF puts in the WHERE clause.
    /// </summary>
    private void IncrementVersionsOfModifiedAggregates()
    {
        var modifiedEntries = dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(entry => entry.State == EntityState.Modified);

        foreach (var entry in modifiedEntries)
        {
            var version = entry.Property(aggregate => aggregate.Version);
            version.CurrentValue = version.OriginalValue + 1;
        }
    }

    /// <summary>
    /// A failed commit leaves the tracker holding changes that never landed. Clearing it
    /// means a retry reloads fresh rows instead of the stale tracked copies.
    /// </summary>
    private void Discard(List<IAggregateRoot> aggregates)
    {
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        dbContext.ChangeTracker.Clear();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    [LoggerMessage(Level = LogLevel.Information, Message = "Write rejected by a unique constraint: {Detail}")]
    private partial void LogUniqueViolation(string detail);
}
