using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.History;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands;

/// <summary>
/// A person's own history. The actor is always the subject: nobody can read or change
/// another person's entries, which the Firestore rules enforced per document.
/// </summary>
internal sealed class HistoryHandlers(
    IHistoryEntryRepository history,
    IRoomRepository rooms,
    IUnitOfWork unitOfWork,
    TimeProvider clock) :
    ICommandHandler<SetFavouriteCommand, Result>,
    ICommandHandler<MarkReadCommand, Result>,
    ICommandHandler<RemoveFromHistoryCommand, Result>,
    ICommandHandler<RecordAttendanceCommand, Result>
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

    /// <summary>
    /// Creates the entry if needed: the dashboard records read position for rooms it only
    /// observes, without ever having joined from the extension.
    /// </summary>
    public Task<Result> HandleAsync(MarkReadCommand command, CancellationToken cancellationToken) =>
        UpsertAsync(command.ActorId, command.RoomId, entry => entry.MarkReadUpTo(command.At), cancellationToken);

    public Task<Result> HandleAsync(RecordAttendanceCommand command, CancellationToken cancellationToken) =>
        UpsertAsync(command.ActorId, command.RoomId, entry => Result.Success(), cancellationToken, attend: true);

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

    private Task<Result> UpsertAsync(UserId userId, RoomId roomId, Func<HistoryEntry, Result> change, CancellationToken cancellationToken, bool attend = false) =>
        ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var room = await rooms.GetByIdAsync(roomId, cancellationToken);
            if (room is null)
            {
                return RoomErrors.NotFound;
            }

            var snapshot = ToAttendedRoom(room);
            var entry = await history.GetAsync(new HistoryEntryId(userId, roomId), cancellationToken);
            var isNew = entry is null;
            entry ??= HistoryEntry.Track(userId, roomId, snapshot);

            if (attend)
            {
                entry.RecordAttendance(snapshot, clock);
            }

            var changed = change(entry);
            if (changed.IsFailure)
            {
                return changed;
            }

            if (isNew)
            {
                history.Add(entry);
            }
            else
            {
                history.Update(entry);
            }

            var committed = await unitOfWork.CommitAsync(cancellationToken);

            // Two clients creating the same entry at once: retry, and the second pass updates it.
            return committed.Error == ConcurrencyErrors.Duplicate ? ConcurrencyErrors.Conflict : committed;
        });

    /// <summary>The Rooms-to-History boundary: copy the fields History keeps, nothing more.</summary>
    private static AttendedRoom ToAttendedRoom(Room room) =>
        new(room.Name.Value, room.Video.PageUrl, room.CreatedAt);
}
