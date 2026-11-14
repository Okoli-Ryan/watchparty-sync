using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Persistence;

/// <summary>
/// Re-runs a load-change-commit cycle when it loses an optimistic-concurrency race.
/// Only for operations that are safe to recompute from fresh state, such as heartbeats
/// and automatic handoff. The attempt must reload the aggregate every time.
/// </summary>
public static class ConcurrencyRetry
{
    public const int MaxAttempts = 3;

    public static async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> attempt)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(attempt);
        TResult result = default!;
        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            result = await attempt();
            if (result.Error != ConcurrencyErrors.Conflict)
            {
                return result;
            }
        }

        return result;
    }
}
