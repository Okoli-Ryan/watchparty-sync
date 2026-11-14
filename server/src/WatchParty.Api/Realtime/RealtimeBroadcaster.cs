using Microsoft.AspNetCore.SignalR;
using WatchParty.Application.History.Queries;
using WatchParty.Application.Rooms.Queries;

namespace WatchParty.Api.Realtime;

/// <summary>
/// Drains <see cref="RealtimeQueue"/> and pushes fresh state to subscribers. It reads the
/// current state once per change and sends that to everyone, instead of having every
/// client refetch. Signals that pile up while a batch is going out are de-duplicated,
/// so a burst of heartbeats costs one members read, not one per beat.
/// </summary>
internal sealed partial class RealtimeBroadcaster(
    RealtimeQueue queue,
    IServiceScopeFactory scopes,
    IHubContext<RealtimeHub> hub,
    TimeProvider clock,
    ILogger<RealtimeBroadcaster> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<RealtimeSignal>();
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            batch.Clear();
            while (batch.Count < 500 && queue.Reader.TryRead(out var signal))
            {
                if (!batch.Contains(signal))
                {
                    batch.Add(signal);
                }
            }

            await using var scope = scopes.CreateAsyncScope();
            foreach (var signal in batch)
            {
                try
                {
                    await SendAsync(scope.ServiceProvider, signal, stoppingToken);
                }
#pragma warning disable CA1031 // One failed push must not stop the loop; logged, and the next change resends.
                catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    LogSendFailed(exception, signal.Kind, signal.RoomId.Value);
                }
            }
        }
    }

    private async Task SendAsync(IServiceProvider services, RealtimeSignal signal, CancellationToken cancellationToken)
    {
        var rooms = services.GetRequiredService<IRoomQueries>();
        switch (signal.Kind)
        {
            case RealtimeSignalKind.Room:
                if (await rooms.GetRoomAsync(signal.RoomId, cancellationToken) is { } room)
                {
                    await hub.Clients.Groups(RealtimeTopics.RoomGroup(signal.RoomId), RealtimeTopics.Rooms).SendAsync("room", room, cancellationToken);
                }

                break;

            case RealtimeSignalKind.Members:
                var members = await rooms.ListMembersAsync(signal.RoomId, cancellationToken);
                var snapshot = new MembersSnapshotDto(signal.RoomId.Value, members, clock.GetUtcNow().ToUnixTimeMilliseconds());
                await hub.Clients.Group(RealtimeTopics.MembersGroup(signal.RoomId)).SendAsync("members", snapshot, cancellationToken);
                break;

            case RealtimeSignalKind.Message when signal.MessageId is { } messageId:
                if (await rooms.GetMessageAsync(messageId, cancellationToken) is { } message)
                {
                    await hub.Clients.Group(RealtimeTopics.ChatGroup(signal.RoomId)).SendAsync("message", message, cancellationToken);
                }

                break;

            case RealtimeSignalKind.History when signal.UserId is { } userId:
                var entry = await services.GetRequiredService<IHistoryQueries>().GetAsync(userId, signal.RoomId, cancellationToken);
                await hub.Clients.Group(RealtimeTopics.HistoryGroup(userId))
                    .SendAsync("history", new HistoryChangeDto(signal.RoomId.Value, entry), cancellationToken);
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime push {Kind} for room {RoomId} failed")]
    private partial void LogSendFailed(Exception exception, RealtimeSignalKind kind, Guid roomId);
}
