using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.History;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.SetFavourite;

/// <summary>Stars or unstars a room in the caller's own history. The entry must already exist.</summary>
internal sealed class SetFavouriteHandler(IHistoryEntryRepository history, IUnitOfWork unitOfWork)
    : ICommandHandler<SetFavouriteCommand, Result>
{
    public Task<Result> HandleAsync(SetFavouriteCommand command, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var entry = await history.GetAsync(new HistoryEntryId(command.ActorId, command.RoomId), cancellationToken);
            if (entry is null)
            {
                return HistoryErrors.NotFound;
            }

            entry.MarkFavourite(command.Favourite);
            history.Update(entry);
            return await unitOfWork.CommitAsync(cancellationToken);
        });
}
