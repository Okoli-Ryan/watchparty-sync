using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

/// <summary>Raised on first join AND on every rejoin: each one is an attendance.</summary>
public sealed record MemberJoined(RoomId RoomId, UserId UserId) : IDomainEvent;
