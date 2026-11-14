using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Realtime;

public enum RealtimeSignalKind
{
    Room,
    Members,
    Message,
    History,
}

/// <summary>A note that something changed. Identical signals queued together are sent once.</summary>
public sealed record RealtimeSignal(RealtimeSignalKind Kind, RoomId RoomId, ChatMessageId? MessageId = null, UserId? UserId = null);
