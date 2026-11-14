namespace WatchParty.Application.Rooms.Queries;

/// <summary>
/// A room's members plus the server's clock at the moment they were read, so a client
/// can age heartbeats against server time instead of its own (DECISIONS.md #6).
/// </summary>
public sealed record MembersSnapshotDto(Guid RoomId, IReadOnlyList<MemberDto> Members, long ServerTime);
