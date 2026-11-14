using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Realtime;
using WatchParty.Domain.History;
using WatchParty.Domain.Rooms.Events;

namespace WatchParty.Application.Realtime;

/// <summary>
/// Turns committed domain events into "this changed" signals for connected clients.
/// The push replaces Firestore's onSnapshot; what each signal carries is decided by
/// the realtime adapter, not here.
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
    public Task HandleAsync(RoomCreated domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(PlaybackRecorded domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(RoomTouched domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(RoomVideoRepointed domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(RoomOwnershipChanged domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(RoomDeactivated domainEvent, CancellationToken cancellationToken) => Room(domainEvent.RoomId);

    public Task HandleAsync(MemberJoined domainEvent, CancellationToken cancellationToken) => Members(domainEvent.RoomId);

    public Task HandleAsync(MemberHeartbeatRecorded domainEvent, CancellationToken cancellationToken) => Members(domainEvent.RoomId);

    public Task HandleAsync(MemberRoleChanged domainEvent, CancellationToken cancellationToken) => Members(domainEvent.RoomId);

    public Task HandleAsync(MemberLeft domainEvent, CancellationToken cancellationToken) => Members(domainEvent.RoomId);

    public Task HandleAsync(ChatMessagePosted domainEvent, CancellationToken cancellationToken)
    {
        notifier.MessagePosted(domainEvent.RoomId, domainEvent.MessageId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(HistoryEntryChanged domainEvent, CancellationToken cancellationToken) => History(domainEvent.UserId, domainEvent.RoomId);

    public Task HandleAsync(HistoryEntryRemoved domainEvent, CancellationToken cancellationToken) => History(domainEvent.UserId, domainEvent.RoomId);

    private Task Room(Domain.SharedKernel.RoomId roomId)
    {
        notifier.RoomChanged(roomId);
        return Task.CompletedTask;
    }

    private Task Members(Domain.SharedKernel.RoomId roomId)
    {
        notifier.MembersChanged(roomId);
        return Task.CompletedTask;
    }

    private Task History(Domain.SharedKernel.UserId userId, Domain.SharedKernel.RoomId roomId)
    {
        notifier.HistoryChanged(userId, roomId);
        return Task.CompletedTask;
    }
}
