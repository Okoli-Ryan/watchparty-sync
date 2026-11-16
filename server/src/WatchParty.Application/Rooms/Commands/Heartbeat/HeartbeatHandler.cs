using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Heartbeat;

/// <summary>
/// Marks a member as still watching, then lets <see cref="RoomSuccession"/> prune anyone
/// who has gone quiet and hand a hostless room to its heir.
/// </summary>
internal sealed class HeartbeatHandler(
    IMembershipRepository memberships,
    RoomEntry entry,
    RoomSuccession succession,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<HeartbeatCommand, Result>
{
    public async Task<Result> HandleAsync(HeartbeatCommand command, CancellationToken cancellationToken)
    {
        var beat = await RecordBeatAsync(command, cancellationToken);

        // Pruned while this browser was asleep (a suspended laptop, a recycled service
        // worker): come back in rather than heartbeating into nothing forever, which is
        // what the Firebase build did.
        if (beat.Error == RoomErrors.NotAMember)
        {
            var rejoined = await entry.EnterAsync(command.ActorId, command.RoomId, cancellationToken);
            beat = rejoined.IsSuccess ? Result.Success() : rejoined.Error!;
        }

        if (beat.IsSuccess)
        {
            await succession.ReconcileAsync(command.RoomId, cancellationToken);
        }

        return beat;
    }

    private Task<Result> RecordBeatAsync(HeartbeatCommand command, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var membership = await memberships.GetAsync(new MembershipId(command.RoomId, command.ActorId), cancellationToken);
            if (membership is null)
            {
                return RoomErrors.NotAMember;
            }

            membership.Beat(clock);
            memberships.Update(membership);
            return await unitOfWork.CommitAsync(cancellationToken);
        });
}
