using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.History;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal sealed class HistoryEntryRepository(WatchPartyDbContext dbContext) : IHistoryEntryRepository
{
    public Task<HistoryEntry?> GetAsync(HistoryEntryId id, CancellationToken cancellationToken) =>
        dbContext.HistoryEntries.SingleOrDefaultAsync(
            entry => entry.UserId == id.UserId && entry.RoomId == id.RoomId,
            cancellationToken);

    public void Add(HistoryEntry entry) => dbContext.HistoryEntries.Add(entry);

    public void Update(HistoryEntry entry) => dbContext.AttachIfDetached(entry);

    public void Remove(HistoryEntry entry) => dbContext.HistoryEntries.Remove(entry);
}
