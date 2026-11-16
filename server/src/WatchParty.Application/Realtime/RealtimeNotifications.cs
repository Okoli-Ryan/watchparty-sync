using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Realtime;
using WatchParty.Domain.History;
using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Realtime;

/// <summary>
/// Turns committed domain events into "this changed" signals for connected clients.
/// The push replaces Firestore's onSnapshot; what each signal carries is decided by
/// the realtime adapter, not here.
///
/// Every domain event that clients should hear about is listed here, in one of four
/// groups. An event with no entry reaches nobody, so a test checks that each event in
/// the domain is either handled here or deliberately exempt.
/// </summary>
internal sealed class RealtimeNotifications(IRealtimeNotifier notifier) :
    IDomainEventHandler<RoomCreated>,
    IDomainEventHandler<PlaybackRecorded>,
    IDomainEventHandler<RoomTouched>,
    IDomainEventHandler<RoomVideoRepointed>,
    IDomainEventHandler<RoomOwnershipChanged>,
    IDomainEventHandler<RoomDeactivated>,
    IDomainEventHandler<MemberJoined>,
    IDomainEventHandler<MemberHeartbeatRecorded>,
    IDomainEventHandler<MemberRoleChanged>,
    IDomainEventHandler<MemberLeft>,
    IDomainEventHandler<ChatMessagePosted>,
    IDomainEventHandler<HistoryEntryChanged>,
    IDomainEventHandler<HistoryEntryRemoved>
{
    // The room itself changed: its video, playback, owner, liveness or whether it still exists.
    public Task HandleAsync(RoomCreated domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    public Task HandleAsync(PlaybackRecorded domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    public Task HandleAsync(RoomTouched domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    public Task HandleAsync(RoomVideoRepointed domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    public Task HandleAsync(RoomOwnershipChanged domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    public Task HandleAsync(RoomDeactivated domainEvent, CancellationToken cancellationToken) => NotifyRoomChanged(domainEvent.RoomId);

    // Who is in the room changed: the roster is resent.
    public Task HandleAsync(MemberJoined domainEvent, CancellationToken cancellationToken) => NotifyMembersChanged(domainEvent.RoomId);

    public Task HandleAsync(MemberHeartbeatRecorded domainEvent, CancellationToken cancellationToken) => NotifyMembersChanged(domainEvent.RoomId);

    public Task HandleAsync(MemberRoleChanged domainEvent, CancellationToken cancellationToken) => NotifyMembersChanged(domainEvent.RoomId);

    public Task HandleAsync(MemberLeft domainEvent, CancellationToken cancellationToken) => NotifyMembersChanged(domainEvent.RoomId);

    // A new chat message: signalled by id, so the adapter sends only that message.
    public Task HandleAsync(ChatMessagePosted domainEvent, CancellationToken cancellationToken)
    {
        notifier.MessagePosted(domainEvent.RoomId, domainEvent.MessageId);
        return Task.CompletedTask;
    }

    // One user's history list changed: only that user hears it.
    public Task HandleAsync(HistoryEntryChanged domainEvent, CancellationToken cancellationToken) => NotifyHistoryChanged(domainEvent.UserId, domainEvent.RoomId);

    public Task HandleAsync(HistoryEntryRemoved domainEvent, CancellationToken cancellationToken) => NotifyHistoryChanged(domainEvent.UserId, domainEvent.RoomId);

    private Task NotifyRoomChanged(RoomId roomId)
    {
        notifier.RoomChanged(roomId);
        return Task.CompletedTask;
    }

    private Task NotifyMembersChanged(RoomId roomId)
    {
        notifier.MembersChanged(roomId);
        return Task.CompletedTask;
    }

    private Task NotifyHistoryChanged(UserId userId, RoomId roomId)
    {
        notifier.HistoryChanged(userId, roomId);
        return Task.CompletedTask;
    }
}
