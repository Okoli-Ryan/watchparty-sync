using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Services;

/// <summary>
/// Gets a user into a room: settles who they are in it (the primary owner takes the host
/// role back on return), then records their membership. Shared by joining and by a
/// heartbeat that finds the member was pruned while their browser slept.
/// </summary>
internal sealed class RoomEntry(
    IUserRepository users,
    IRoomRepository rooms,
    IMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<MemberRole>> EnterAsync(UserId actorId, RoomId roomId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(actorId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var role = await ResolveRoleAsync(user, roomId, cancellationToken);
        if (role.IsFailure)
        {
            return role;
        }

        var saved = await SaveMembershipAsync(user, roomId, role.Value, cancellationToken);
        return saved.IsFailure ? saved.Error! : role.Value;
    }

    /// <summary>
    /// The role the user holds on arrival. The primary owner reclaims the room: automatic
    /// handoff never moves that claim, so a host who refreshes or steps away gets their
    /// room back. Everyone else, including a successor, is whatever the room says they are.
    /// </summary>
    private Task<Result<MemberRole>> ResolveRoleAsync(User user, RoomId roomId, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(async Task<Result<MemberRole>> () =>
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

            if (room.PrimaryOwnerId == user.Id)
            {
                var reclaimed = room.Reclaim(user.Id, user.DisplayName);
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

            return room.IsOwnedBy(user.Id) ? MemberRole.Owner : MemberRole.Viewer;
        });

    /// <summary>
    /// Adds the membership, or refreshes the existing one. Two tabs joining at once both
    /// see "no membership"; the loser's insert hits the primary key (Duplicate), and the
    /// second pass finds the row and rejoins instead.
    /// </summary>
    private Task<Result> SaveMembershipAsync(User user, RoomId roomId, MemberRole role, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(
            async Task<Result> () =>
            {
                var membership = await memberships.GetAsync(new MembershipId(roomId, user.Id), cancellationToken);
                if (membership is null)
                {
                    memberships.Add(Membership.Join(roomId, user.Id, user.DisplayName, role, clock));
                }
                else
                {
                    membership.Rejoin(user.DisplayName, role, clock);
                    memberships.Update(membership);
                }

                return await unitOfWork.CommitAsync(cancellationToken);
            },
            [ConcurrencyErrors.Conflict, ConcurrencyErrors.Duplicate]);
}
