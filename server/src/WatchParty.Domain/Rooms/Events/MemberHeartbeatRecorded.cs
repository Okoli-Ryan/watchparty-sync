using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

public sealed record MemberHeartbeatRecorded(RoomId RoomId, UserId UserId) : IDomainEvent;
