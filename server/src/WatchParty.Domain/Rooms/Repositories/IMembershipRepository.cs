using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Repositories;

public interface IMembershipRepository
{
    Task<Membership?> GetAsync(MembershipId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Membership>> ListByRoomAsync(RoomId roomId, CancellationToken cancellationToken);

    void Add(Membership membership);

    void Update(Membership membership);

    void Remove(Membership membership);
}
