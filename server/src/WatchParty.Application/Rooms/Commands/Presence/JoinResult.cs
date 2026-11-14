using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Rooms.Commands.Presence;

/// <param name="Role">The role the caller holds after joining, which may be Owner if they reclaimed the room.</param>
public sealed record JoinResult(MemberRole Role);
