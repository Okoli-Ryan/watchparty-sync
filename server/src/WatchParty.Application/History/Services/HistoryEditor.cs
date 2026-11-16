using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.History;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Services;

/// <summary>
/// Changes a person's history entry for a room, creating it the first time. A person's
/// history is their own: callers always pass the actor as <c>userId</c>, so nobody can
/// read or change another person's entries.
/// </summary>
internal sealed class HistoryEditor(
    IHistoryEntryRepository history,
    IRoomRepository rooms,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <summary>Applies <paramref name="change"/> to the entry, creating it if it does not exist yet.</summary>
    public Task<Result> UpsertAsync(UserId userId, RoomId roomId, Func<HistoryEntry, Result> change, CancellationToken cancellationToken) =>
        UpsertCoreAsync(userId, roomId, attend: false, change, cancellationToken);

    /// <summary>Records that the user attended the room now, creating the entry if needed.</summary>
    public Task<Result> RecordAttendanceAsync(UserId userId, RoomId roomId, CancellationToken cancellationToken) =>
        UpsertCoreAsync(userId, roomId, attend: true, _ => Result.Success(), cancellationToken);

    private Task<Result> UpsertCoreAsync(UserId userId, RoomId roomId, bool attend, Func<HistoryEntry, Result> change, CancellationToken cancellationToken) =>
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
