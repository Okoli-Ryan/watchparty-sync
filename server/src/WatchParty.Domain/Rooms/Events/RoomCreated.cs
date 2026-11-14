using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

public sealed record RoomCreated(RoomId RoomId, UserId OwnerId) : IDomainEvent;
