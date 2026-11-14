using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Realtime;

/// <summary>
/// Tells connected clients that something changed. Implementations only enqueue, never
/// block: the request that caused the change has already committed and must not wait
/// on fan-out. Delivery is best effort, and clients refetch on reconnect.
/// </summary>
public interface IRealtimeNotifier
{
    void RoomChanged(RoomId roomId);

    void MembersChanged(RoomId roomId);

    void MessagePosted(RoomId roomId, ChatMessageId messageId);

    void HistoryChanged(UserId userId, RoomId roomId);
}
