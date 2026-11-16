using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.TransferOwnership;

/// <summary>Deliberately hands the room to another member who is watching right now.</summary>
internal sealed class TransferOwnershipHandler(RoomEditor editor, IMembershipRepository memberships, TimeProvider clock)
    : ICommandHandler<TransferOwnershipCommand, Result>
{
    public async Task<Result> HandleAsync(TransferOwnershipCommand command, CancellationToken cancellationToken)
    {
        // Only someone actually watching can drive playback, so a target whose heartbeat
        // has lapsed is refused rather than handed a room nobody is steering.
        var target = await memberships.GetAsync(new MembershipId(command.RoomId, command.TargetId), cancellationToken);
        if (target is null || !target.IsPresentAt(clock.GetUtcNow()))
        {
            return RoomErrors.TransferTargetAbsent;
        }

        return await editor.ChangeAsync(command.RoomId, room => room.TransferTo(command.ActorId, target.UserId, target.DisplayName), cancellationToken);
    }
}
