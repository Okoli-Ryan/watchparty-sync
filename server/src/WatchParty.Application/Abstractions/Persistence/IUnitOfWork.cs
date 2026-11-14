using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Persistence;

/// <summary>
/// Commits what the repositories recorded, then dispatches the aggregates' domain events.
/// One aggregate per commit (§2.3): a stale concurrency token comes back as
/// <see cref="ConcurrencyErrors.Conflict"/>, never as an exception.
/// </summary>
public interface IUnitOfWork
{
    Task<Result> CommitAsync(CancellationToken cancellationToken);
}
