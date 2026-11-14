namespace WatchParty.Domain.SharedKernel;

/// <summary>Marker so infrastructure can collect events without knowing the id type.</summary>
public interface IAggregateRoot
{
    /// <summary>Optimistic-concurrency token: the version this instance was loaded at.</summary>
    long Version { get; }

    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>
/// The single entry point to a consistency boundary. Collects domain events, which
/// infrastructure dispatches only after the aggregate has been persisted.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public long Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
