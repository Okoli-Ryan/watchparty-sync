using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Services;

/// <summary>
/// Load a room, apply one change to it, commit. Every host action is an absolute write
/// (a state, not a delta), so losing a race to another write is safely retried against
/// fresh state. The playback write and the 9-second room touch are the usual pair that
/// collide.
/// </summary>
internal sealed class RoomEditor(IRoomRepository rooms, IUnitOfWork unitOfWork)
{
    public Task<Result> ChangeAsync(RoomId roomId, Func<Room, Result> change, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var room = await rooms.GetByIdAsync(roomId, cancellationToken);
            if (room is null)
            {
                return RoomErrors.NotFound;
            }

            var changed = change(room);
            if (changed.IsFailure)
            {
                return changed;
            }

            rooms.Update(room);
            return await unitOfWork.CommitAsync(cancellationToken);
        });
}
