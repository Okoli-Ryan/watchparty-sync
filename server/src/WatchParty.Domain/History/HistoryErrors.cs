using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.History;

public static class HistoryErrors
{
    public static readonly Error NotFound = Error.NotFound("History.NotFound", "That room is not in your history.");

    public static readonly Error InvalidReadPosition =
        Error.Validation("History.InvalidReadPosition", "The read position must be a non-negative epoch time in milliseconds.");
}
