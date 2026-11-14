using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.History.Queries;
using WatchParty.Domain.History;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Queries;

internal sealed class HistoryQueries(WatchPartyDbContext dbContext) : IHistoryQueries
{
    public async Task<IReadOnlyList<HistoryEntryDto>> ListAsync(UserId userId, int limit, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(limit, 1, IHistoryQueries.MaxPage);

        return await dbContext.HistoryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .Where(entry => entry.LastAttendedAt != null)
            .OrderByDescending(entry => entry.LastAttendedAt)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public Task<HistoryEntryDto?> GetAsync(UserId userId, RoomId roomId, CancellationToken cancellationToken) =>
        dbContext.HistoryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.RoomId == roomId)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);

    private static readonly Expression<Func<HistoryEntry, HistoryEntryDto>> ToDto = entry => new HistoryEntryDto(
        entry.RoomId.Value,
        entry.Room.Name,
        entry.Room.PageUrl,
        entry.IsFavourite,
        entry.LastAttendedAt == null ? null : entry.LastAttendedAt.Value.ToUnixTimeMilliseconds(),
        entry.Room.CreatedAt == null ? null : entry.Room.CreatedAt.Value.ToUnixTimeMilliseconds(),
        entry.LastReadAt);
}
