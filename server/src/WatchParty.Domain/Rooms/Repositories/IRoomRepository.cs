using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken);

    void Add(Room room);

    void Update(Room room);
}
