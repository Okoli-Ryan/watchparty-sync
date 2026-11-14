using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Persistence;

public static class ConcurrencyErrors
{
    public static readonly Error Conflict =
        Error.Conflict("Concurrency.Conflict", "Someone else changed this at the same moment. Try again.");

    public static readonly Error Duplicate =
        Error.Conflict("Concurrency.Duplicate", "That already exists.");
}
