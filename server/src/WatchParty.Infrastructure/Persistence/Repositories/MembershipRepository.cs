using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal sealed class MembershipRepository(WatchPartyDbContext dbContext) : IMembershipRepository
{
    public Task<Membership?> GetAsync(MembershipId id, CancellationToken cancellationToken) =>
        dbContext.Memberships.SingleOrDefaultAsync(
            membership => membership.RoomId == id.RoomId && membership.UserId == id.UserId,
            cancellationToken);

    public async Task<IReadOnlyList<Membership>> ListByRoomAsync(RoomId roomId, CancellationToken cancellationToken) =>
        await dbContext.Memberships
            .Where(membership => membership.RoomId == roomId)
            .ToListAsync(cancellationToken);

    public void Add(Membership membership) => dbContext.Memberships.Add(membership);

    public void Update(Membership membership) => dbContext.AttachIfDetached(membership);

    public void Remove(Membership membership) => dbContext.Memberships.Remove(membership);
}
