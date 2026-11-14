using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

/// <summary>Someone else now hosts the room.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="PreviousOwnerId">Who hosted before.</param>
/// <param name="NewOwnerId">Who hosts now.</param>
/// <param name="Deliberate">True for a manual transfer, which also moves the persistent claim.</param>
public sealed record RoomOwnershipChanged(RoomId RoomId, UserId PreviousOwnerId, UserId NewOwnerId, bool Deliberate) : IDomainEvent;
