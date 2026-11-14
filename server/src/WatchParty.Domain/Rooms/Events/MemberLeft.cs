using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

/// <summary>A member left the room.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="UserId">Who left.</param>
/// <param name="Pruned">True when removed for a stale heartbeat rather than by leaving.</param>
public sealed record MemberLeft(RoomId RoomId, UserId UserId, bool Pruned) : IDomainEvent;
