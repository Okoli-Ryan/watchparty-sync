using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Persistence;

/// <summary>
/// Re-runs a load-change-commit cycle when it loses an optimistic-concurrency race.
/// Only for operations that are safe to recompute from fresh state, such as heartbeats
/// and automatic handoff. The attempt must reload the aggregate every time: a failed
/// commit clears the change tracker, so a retry that reuses a loaded aggregate would
/// write stale state.
/// </summary>
public static class ConcurrencyRetry
{
    public const int MaxAttempts = 3;

    /// <summary>Retries when the commit loses a version check (<see cref="ConcurrencyErrors.Conflict"/>).</summary>
    public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> attempt)
        where TResult : Result =>
        RunAsync(attempt, [ConcurrencyErrors.Conflict]);

    /// <summary>
    /// Retries on any of <paramref name="retryOn"/>. Use this to also retry
    /// <see cref="ConcurrencyErrors.Duplicate"/> for an insert that can race with the
    /// same insert: the second pass finds the row and updates it instead.
    /// </summary>
    public static async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> attempt, IReadOnlyCollection<Error> retryOn)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(retryOn);

        TResult result = default!;
        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            result = await attempt();
            if (result.Error is null || !retryOn.Contains(result.Error))
            {
                return result;
            }
        }

        return result;
    }
}
