using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Configurations;

/// <summary>What every aggregate root shares: an optimistic-concurrency token, and no mapped events.</summary>
internal static class AggregateMapping
{
    public static void MapAggregateRoot<TAggregate>(this EntityTypeBuilder<TAggregate> builder)
        where TAggregate : class, IAggregateRoot
    {
        // UnitOfWork increments it on every update; a stale copy then updates no row,
        // which EF reports as a concurrency conflict (409 to the client).
        builder.Property(aggregate => aggregate.Version).IsConcurrencyToken();
        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
