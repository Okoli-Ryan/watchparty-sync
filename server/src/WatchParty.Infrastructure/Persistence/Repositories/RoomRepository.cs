using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(WatchPartyDbContext dbContext) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken) =>
        dbContext.Rooms.SingleOrDefaultAsync(room => room.Id == id, cancellationToken);

    public void Add(Room room) => dbContext.Rooms.Add(room);

    /// <summary>A loaded room is already tracked; only a detached one needs attaching.</summary>
    public void Update(Room room) => dbContext.AttachIfDetached(room);
}
