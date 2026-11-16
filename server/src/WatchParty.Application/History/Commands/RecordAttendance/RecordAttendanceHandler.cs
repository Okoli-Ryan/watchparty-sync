using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.RecordAttendance;

/// <summary>Notes that the caller attended a room. Triggered when someone joins one.</summary>
internal sealed class RecordAttendanceHandler(HistoryEditor editor) : ICommandHandler<RecordAttendanceCommand, Result>
{
    public Task<Result> HandleAsync(RecordAttendanceCommand command, CancellationToken cancellationToken) =>
        editor.RecordAttendanceAsync(command.ActorId, command.RoomId, cancellationToken);
}
