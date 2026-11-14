using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

public readonly record struct MembershipId(RoomId RoomId, UserId UserId);
