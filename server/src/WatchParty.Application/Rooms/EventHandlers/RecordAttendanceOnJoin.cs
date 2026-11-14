using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Commands;
using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.EventHandlers;

/// <summary>
/// Joining a room (Rooms context) stamps "I was here" in the person's history (History
/// context): a separate aggregate, so a separate commit, after the join has landed.
/// The client used to make this write itself; now it cannot forget or forge it.
/// </summary>
internal sealed partial class RecordAttendanceOnJoin(
    ICommandHandler<RecordAttendanceCommand, Result> recordAttendance,
    ILogger<RecordAttendanceOnJoin> logger) : IDomainEventHandler<MemberJoined>
{
    public async Task HandleAsync(MemberJoined domainEvent, CancellationToken cancellationToken)
    {
        var result = await recordAttendance.HandleAsync(new RecordAttendanceCommand(domainEvent.UserId, domainEvent.RoomId), cancellationToken);
        if (result.IsFailure)
        {
            LogFailed(domainEvent.RoomId.Value, result.Error!.Code);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not record attendance for room {RoomId}: {ErrorCode}")]
    private partial void LogFailed(Guid roomId, string errorCode);
}
