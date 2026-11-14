using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.EventHandlers;

/// <summary>
/// Keeps each membership's role in step with the room's owner, whichever way ownership
/// moved (handoff, reclaim, transfer). The room is the source of truth; roles on
/// memberships exist for the roster.
/// </summary>
internal sealed class SyncMemberRolesOnOwnershipChange(IMembershipRepository memberships, IUnitOfWork unitOfWork)
    : IDomainEventHandler<RoomOwnershipChanged>
{
    public async Task HandleAsync(RoomOwnershipChanged domainEvent, CancellationToken cancellationToken)
    {
        await AssignAsync(domainEvent.RoomId, domainEvent.PreviousOwnerId, MemberRole.Viewer, cancellationToken);
        await AssignAsync(domainEvent.RoomId, domainEvent.NewOwnerId, MemberRole.Owner, cancellationToken);
    }

    private Task<Result> AssignAsync(RoomId roomId, UserId userId, MemberRole role, CancellationToken cancellationToken) =>
        ConcurrencyRetry.RunAsync(async Task<Result> () =>
        {
            var membership = await memberships.GetAsync(new MembershipId(roomId, userId), cancellationToken);
            if (membership is null || membership.Role == role)
            {
                return Result.Success();
            }

            membership.AssumeRole(role);
            memberships.Update(membership);
            return await unitOfWork.CommitAsync(cancellationToken);
        });
}
