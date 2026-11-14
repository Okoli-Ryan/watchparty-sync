using System.Threading.Channels;
using WatchParty.Application.Abstractions.Realtime;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Realtime;

/// <summary>
/// <see cref="IRealtimeNotifier"/> as an in-memory queue. Writers never wait: the change is
/// already committed, and fan-out happens on <see cref="RealtimeBroadcaster"/>. Bounded,
/// dropping the oldest signal when full. A dropped signal costs one stale view until the
/// next change, and clients refetch everything when they reconnect.
/// </summary>
public sealed class RealtimeQueue : IRealtimeNotifier
{
    private readonly Channel<RealtimeSignal> _channel = Channel.CreateBounded<RealtimeSignal>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<RealtimeSignal> Reader => _channel.Reader;

    public void RoomChanged(RoomId roomId) => Write(new RealtimeSignal(RealtimeSignalKind.Room, roomId));

    public void MembersChanged(RoomId roomId) => Write(new RealtimeSignal(RealtimeSignalKind.Members, roomId));

    public void MessagePosted(RoomId roomId, ChatMessageId messageId) =>
        Write(new RealtimeSignal(RealtimeSignalKind.Message, roomId, MessageId: messageId));

    public void HistoryChanged(UserId userId, RoomId roomId) =>
        Write(new RealtimeSignal(RealtimeSignalKind.History, roomId, UserId: userId));

    private void Write(RealtimeSignal signal) => _channel.Writer.TryWrite(signal);
}
