using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.RecordPlayback;

/// <summary>The host played, paused, seeked or changed rate.</summary>
internal sealed class RecordPlaybackHandler(RoomEditor editor, TimeProvider clock)
    : ICommandHandler<RecordPlaybackCommand, Result>
{
    public Task<Result> HandleAsync(RecordPlaybackCommand command, CancellationToken cancellationToken) =>
        editor.ChangeAsync(
            command.RoomId,
            room => room.RecordPlayback(command.ActorId, command.IsPlaying, command.CurrentTime, command.Rate, clock),
            cancellationToken);
}
