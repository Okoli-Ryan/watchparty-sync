using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Events;

/// <summary>The host played, paused, seeked or changed rate.</summary>
public sealed record PlaybackRecorded(RoomId RoomId) : IDomainEvent;
