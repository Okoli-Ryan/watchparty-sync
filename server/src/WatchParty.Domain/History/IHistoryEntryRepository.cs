namespace WatchParty.Domain.History;

public interface IHistoryEntryRepository
{
    Task<HistoryEntry?> GetAsync(HistoryEntryId id, CancellationToken cancellationToken);

    void Add(HistoryEntry entry);

    void Update(HistoryEntry entry);

    void Remove(HistoryEntry entry);
}
