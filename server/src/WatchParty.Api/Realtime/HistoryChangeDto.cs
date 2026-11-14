using WatchParty.Application.History.Queries;

namespace WatchParty.Api.Realtime;

/// <param name="RoomId">The room whose entry changed.</param>
/// <param name="Entry">The entry now, or null when it was removed.</param>
public sealed record HistoryChangeDto(Guid RoomId, HistoryEntryDto? Entry);
