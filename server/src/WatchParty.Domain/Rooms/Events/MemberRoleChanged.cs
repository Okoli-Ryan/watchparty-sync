using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.Rooms;

namespace WatchParty.Domain.Rooms.Events;

public sealed record MemberRoleChanged(RoomId RoomId, UserId UserId, MemberRole Role) : IDomainEvent;
