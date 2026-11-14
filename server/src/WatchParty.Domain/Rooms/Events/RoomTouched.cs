using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

/// <summary>The host refreshed the room's liveness heartbeat (and maybe its playhead).</summary>
public sealed record RoomTouched(RoomId RoomId) : IDomainEvent;
