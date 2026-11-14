using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Presence;

/// <summary>
/// Joining, heartbeating and leaving. Each of these can change who should host, so each
/// ends by asking <see cref="RoomSuccession"/> to reconcile the room on the server.
/// </summary>
internal sealed class PresenceHandlers(
    IUserRepository users,
    IRoomRepository rooms,
    IMembershipRepository memberships,
    RoomSuccession succession,
    IUnitOfWork unitOfWork,
    TimeProvider clock) :
    ICommandHandler<JoinRoomCommand, Result<JoinResult>>,
    ICommandHandler<HeartbeatCommand, Result>,
    ICommandHandler<LeaveRoomCommand, Result>
{
    public async Task<Result<JoinResult>> HandleAsync(JoinRoomCommand command, CancellationToken cancellationToken)
    {
        var joined = await EnterAsync(command.ActorId, command.RoomId, cancellationToken);
        if (joined.IsSuccess)
        {
            await succession.ReconcileAsync(command.RoomId, cancellationToken);
        }

        return joined;
    }

    public async Task<Result> HandleAsync(HeartbeatCommand command, CancellationToken cancellationToken)
    {
        var beat = await ConcurrencyRetry.RunAsync(async Task<Result> () =>
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

        // Pruned while this browser was asleep (a suspended laptop, a recycled service
        // worker): come back in rather than heartbeating into nothing forever, which is
        // what the Firebase build did.
        if (beat.Error == RoomErrors.NotAMember)
        {
            var rejoined = await EnterAsync(command.ActorId, command.RoomId, cancellationToken);
            beat = rejoined.IsSuccess ? Result.Success() : rejoined.Error!;
        }

        if (beat.IsSuccess)
        {
            await succession.ReconcileAsync(command.RoomId, cancellationToken);
        }

        return beat;
    }

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

        // Leaving is idempotent, and still reconciles: if the host just left, the next
        // person in line takes over now instead of after the heartbeat grace expires.
        await succession.ReconcileAsync(command.RoomId, cancellationToken);
        return Result.Success();
    }

    private async Task<Result<JoinResult>> EnterAsync(UserId actorId, RoomId roomId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(actorId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // The primary owner reclaims the room on return. Automatic handoff never moves
        // that claim, so a host who refreshes or steps away gets their room back.
        var role = await ConcurrencyRetry.RunAsync(async Task<Result<MemberRole>> () =>
        {
            var room = await rooms.GetByIdAsync(roomId, cancellationToken);
            if (room is null)
            {
                return RoomErrors.NotFound;
            }

            if (!room.IsActive)
            {
                return RoomErrors.Inactive;
            }

            if (room.PrimaryOwnerId == actorId)
            {
                var reclaimed = room.Reclaim(actorId, user.DisplayName);
                if (reclaimed is { IsSuccess: true, Value: true })
                {
                    rooms.Update(room);
                    var committed = await unitOfWork.CommitAsync(cancellationToken);
                    if (committed.IsFailure)
                    {
                        return committed.Error!;
                    }
                }
            }

            return room.IsOwnedBy(actorId) ? MemberRole.Owner : MemberRole.Viewer;
        });

        if (role.IsFailure)
        {
            return role.Error!;
        }

        // Two tabs joining at once both see "no membership"; the loser's insert hits the
        // primary key, and a second pass finds the row and rejoins instead.
        Result saved = Result.Success();
        for (var attempt = 0; attempt < ConcurrencyRetry.MaxAttempts; attempt++)
        {
            var membership = await memberships.GetAsync(new MembershipId(roomId, actorId), cancellationToken);
            if (membership is null)
            {
                memberships.Add(Membership.Join(roomId, actorId, user.DisplayName, role.Value, clock));
            }
            else
            {
                membership.Rejoin(user.DisplayName, role.Value, clock);
                memberships.Update(membership);
            }

            saved = await unitOfWork.CommitAsync(cancellationToken);
            if (saved.Error != ConcurrencyErrors.Duplicate && saved.Error != ConcurrencyErrors.Conflict)
            {
                break;
            }
        }

        return saved.IsFailure ? saved.Error! : new JoinResult(role.Value);
    }
}
