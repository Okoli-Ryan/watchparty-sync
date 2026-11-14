using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.HostActions;

/// <summary>
/// Things only the current host may do. Every one is an absolute write (a state, not a
/// delta), so losing a race to another write is safely retried against fresh state.
/// The playback write and the 9-second room touch are the usual pair that collide.
/// </summary>
internal sealed class HostActionHandlers(
    IRoomRepository rooms,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider clock) :
    ICommandHandler<RecordPlaybackCommand, Result>,
    ICommandHandler<TouchRoomCommand, Result>,
    ICommandHandler<RepointVideoCommand, Result>,
    ICommandHandler<TransferOwnershipCommand, Result>
{
    public Task<Result> HandleAsync(RecordPlaybackCommand command, CancellationToken cancellationToken) =>
        ChangeRoomAsync(command.RoomId, room => room.RecordPlayback(command.ActorId, command.IsPlaying, command.CurrentTime, command.Rate, clock), cancellationToken);

    public Task<Result> HandleAsync(TouchRoomCommand command, CancellationToken cancellationToken)
    {
        (double, bool)? position = command is { CurrentTime: { } time, IsPlaying: { } playing } ? (time, playing) : null;
        return ChangeRoomAsync(command.RoomId, room => room.Touch(command.ActorId, position, clock), cancellationToken);
    }

    public Task<Result> HandleAsync(RepointVideoCommand command, CancellationToken cancellationToken)
    {
        var video = VideoSource.Create(command.PageUrl, command.Selector, command.FrameOrigin);
        if (video.IsFailure)
        {
            return Task.FromResult<Result>(video.Error!);
        }

        return ChangeRoomAsync(command.RoomId, room => room.RepointVideo(command.ActorId, video.Value, command.CurrentTime, clock), cancellationToken);
    }

    public async Task<Result> HandleAsync(TransferOwnershipCommand command, CancellationToken cancellationToken)
    {
        // Only someone actually watching can drive playback, so a target whose heartbeat
        // has lapsed is refused rather than handed a room nobody is steering.
        var target = await memberships.GetAsync(new MembershipId(command.RoomId, command.TargetId), cancellationToken);
        if (target is null || !target.IsPresentAt(clock.GetUtcNow()))
        {
            return RoomErrors.TransferTargetAbsent;
        }

        return await ChangeRoomAsync(command.RoomId, room => room.TransferTo(command.ActorId, target.UserId, target.DisplayName), cancellationToken);
    }

    private Task<Result> ChangeRoomAsync(RoomId roomId, Func<Room, Result> change, CancellationToken cancellationToken) =>
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
