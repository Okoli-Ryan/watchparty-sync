using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.RepointVideo;

/// <summary>Points the room at a different video.</summary>
internal sealed class RepointVideoHandler(RoomEditor editor, TimeProvider clock)
    : ICommandHandler<RepointVideoCommand, Result>
{
    public Task<Result> HandleAsync(RepointVideoCommand command, CancellationToken cancellationToken)
    {
        var video = VideoSource.Create(command.PageUrl, command.Selector, command.FrameOrigin);
        if (video.IsFailure)
        {
            return Task.FromResult<Result>(video.Error!);
        }

        return editor.ChangeAsync(command.RoomId, room => room.RepointVideo(command.ActorId, video.Value, command.CurrentTime, clock), cancellationToken);
    }
}
