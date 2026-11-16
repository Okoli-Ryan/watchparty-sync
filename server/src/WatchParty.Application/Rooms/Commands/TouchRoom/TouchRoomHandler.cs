using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.TouchRoom;

/// <summary>The host's liveness heartbeat, optionally carrying their playhead.</summary>
internal sealed class TouchRoomHandler(RoomEditor editor, TimeProvider clock)
    : ICommandHandler<TouchRoomCommand, Result>
{
    public Task<Result> HandleAsync(TouchRoomCommand command, CancellationToken cancellationToken)
    {
        (double, bool)? position = command is { CurrentTime: { } time, IsPlaying: { } playing } ? (time, playing) : null;
        return editor.ChangeAsync(command.RoomId, room => room.Touch(command.ActorId, position, clock), cancellationToken);
    }
}
