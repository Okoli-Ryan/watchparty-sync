namespace WatchParty.Application.History.Queries;

public sealed record HistoryEntryDto(
    Guid RoomId,
    string RoomName,
    string PageUrl,
    bool Favourite,
    long? LastAttendedAt,
    long? RoomCreatedAt,
    long? LastReadAt);
