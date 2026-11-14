namespace WatchParty.Application.Rooms.Queries;

/// <summary>
/// A room as clients see it. Field names match the extension's <c>Room</c> type, and
/// every time is epoch milliseconds of the SERVER clock.
/// </summary>
public sealed record RoomDto(
    Guid Id,
    string Name,
    string PageUrl,
    string Selector,
    string FrameOrigin,
    string Visibility,
    string ChatKey,
    string ChatSalt,
    EncryptedTextDto? ChatCheck,
    Guid OwnerUid,
    string OwnerName,
    Guid PrimaryOwnerUid,
    string PrimaryOwnerName,
    bool IsActive,
    PlaybackDto Playback,
    HostPositionDto? HostPosition,
    long LastActiveAt,
    long CreatedAt);
