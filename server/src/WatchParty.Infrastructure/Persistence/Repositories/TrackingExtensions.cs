using Microsoft.EntityFrameworkCore;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal static class TrackingExtensions
{
    /// <summary>
    /// Repositories hand out tracked aggregates, so EF already sees their changes. An
    /// aggregate that arrives detached is attached as modified so its changes are saved.
    /// </summary>
    public static void AttachIfDetached<TEntity>(this DbContext dbContext, TEntity entity)
        where TEntity : class
    {
        if (dbContext.Entry(entity).State == EntityState.Detached)
        {
            dbContext.Update(entity);
        }
    }
}
