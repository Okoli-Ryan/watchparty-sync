using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.LeaveRoom;

/// <summary>Removes a member. Idempotent: leaving a room you are not in succeeds.</summary>
internal sealed class LeaveRoomHandler(
    IMembershipRepository memberships,
    RoomSuccession succession,
    IUnitOfWork unitOfWork) : ICommandHandler<LeaveRoomCommand, Result>
{
    public async Task<Result> HandleAsync(LeaveRoomCommand command, CancellationToken cancellationToken)
    {
        var membership = await memberships.GetAsync(new MembershipId(command.RoomId, command.ActorId), cancellationToken);
        if (membership is not null)
        {
            membership.Leave(pruned: false);
            memberships.Remove(membership);
            var committed = await unitOfWork.CommitAsync(cancellationToken);
            if (committed.IsFailure)
            {
                return committed;
            }
        }

        // Still reconciles when there was nothing to remove: if the host just left, the
        // next person in line takes over now instead of after the heartbeat grace expires.
        await succession.ReconcileAsync(command.RoomId, cancellationToken);
        return Result.Success();
    }
}
