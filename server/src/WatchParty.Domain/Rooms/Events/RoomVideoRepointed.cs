using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

public sealed record RoomVideoRepointed(RoomId RoomId) : IDomainEvent;
