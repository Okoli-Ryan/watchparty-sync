using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.JoinRoom;

/// <summary>Joins a room, then lets <see cref="RoomSuccession"/> settle who should be hosting.</summary>
internal sealed class JoinRoomHandler(RoomEntry entry, RoomSuccession succession)
    : ICommandHandler<JoinRoomCommand, Result<JoinResult>>
{
    public async Task<Result<JoinResult>> HandleAsync(JoinRoomCommand command, CancellationToken cancellationToken)
    {
        var role = await entry.EnterAsync(command.ActorId, command.RoomId, cancellationToken);
        if (role.IsFailure)
        {
            return role.Error!;
        }

        await succession.ReconcileAsync(command.RoomId, cancellationToken);
        return new JoinResult(role.Value);
    }
}
