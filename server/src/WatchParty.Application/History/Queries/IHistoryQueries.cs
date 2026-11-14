using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Queries;

public interface IHistoryQueries
{
    public const int MaxPage = 200;

    /// <summary>
    /// Attended rooms, most recent first. Entries that only carry a read position (a room
    /// observed from the dashboard but never joined) are left out, as they always were.
    /// </summary>
    Task<IReadOnlyList<HistoryEntryDto>> ListAsync(UserId userId, int limit, CancellationToken cancellationToken);

    Task<HistoryEntryDto?> GetAsync(UserId userId, RoomId roomId, CancellationToken cancellationToken);
}
