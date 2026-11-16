using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.MarkRead;

/// <summary>
/// Records how far the caller has read a room's chat. Creates the history entry if needed:
/// the dashboard records read position for rooms it only observes, without ever having
/// joined from the extension.
/// </summary>
internal sealed class MarkReadHandler(HistoryEditor editor) : ICommandHandler<MarkReadCommand, Result>
{
    public Task<Result> HandleAsync(MarkReadCommand command, CancellationToken cancellationToken) =>
        editor.UpsertAsync(command.ActorId, command.RoomId, entry => entry.MarkReadUpTo(command.At), cancellationToken);
}
