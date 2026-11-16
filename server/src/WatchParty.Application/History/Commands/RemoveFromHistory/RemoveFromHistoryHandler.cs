using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.History;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.RemoveFromHistory;

/// <summary>Removes a room from the caller's own history. Idempotent: removing a missing entry succeeds.</summary>
internal sealed class RemoveFromHistoryHandler(IHistoryEntryRepository history, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveFromHistoryCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveFromHistoryCommand command, CancellationToken cancellationToken)
    {
        var entry = await history.GetAsync(new HistoryEntryId(command.ActorId, command.RoomId), cancellationToken);
        if (entry is null)
        {
            return Result.Success();
        }

        entry.Forget();
        history.Remove(entry);
        return await unitOfWork.CommitAsync(cancellationToken);
    }
}
