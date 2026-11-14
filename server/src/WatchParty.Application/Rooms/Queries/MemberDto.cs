namespace WatchParty.Application.Rooms.Queries;

public sealed record MemberDto(Guid Uid, string DisplayName, string Role, long JoinedAt, long LastSeen);
