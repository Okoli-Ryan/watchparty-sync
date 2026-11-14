using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.Rooms.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Services;

/// <summary>
/// Applies <see cref="OwnershipSuccession"/>: prunes lapsed members, hands a hostless
/// room to its heir, and ends a room nobody is left in. Idempotent, so it runs after
/// every join, heartbeat and leave. Racing reconciles converge: the loser of the room's
/// concurrency check reloads and finds the work already done.
/// </summary>
internal sealed class RoomSuccession(
    IRoomRepository rooms,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task ReconcileAsync(RoomId roomId, CancellationToken cancellationToken)
    {
        await ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var room = await rooms.GetByIdAsync(roomId, cancellationToken);
            if (room is not { IsActive: true })
            {
                return Result.Success();
            }

            var members = await memberships.ListByRoomAsync(roomId, cancellationToken);
            var plan = OwnershipSuccession.Plan(room, members, clock.GetUtcNow());

            if (plan.Stale.Count > 0)
            {
                foreach (var stale in plan.Stale)
                {
                    stale.Leave(pruned: true);
                    memberships.Remove(stale);
                }

                var pruned = await unitOfWork.CommitAsync(cancellationToken);
                if (pruned.IsFailure)
                {
                    return pruned;
                }
            }

            if (plan.Heir is { } heir)
            {
                room.SucceedTo(heir.UserId, heir.DisplayName);
            }
            else if (members.Count == plan.Stale.Count)
            {
                // Nobody is left: the last person out turns off the lights.
                room.Deactivate();
            }
            else
            {
                return Result.Success();
            }

            rooms.Update(room);
            return await unitOfWork.CommitAsync(cancellationToken);
        });
    }
}
