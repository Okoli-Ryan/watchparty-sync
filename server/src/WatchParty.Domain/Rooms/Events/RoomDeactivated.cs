using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

public sealed record RoomDeactivated(RoomId RoomId) : IDomainEvent;
