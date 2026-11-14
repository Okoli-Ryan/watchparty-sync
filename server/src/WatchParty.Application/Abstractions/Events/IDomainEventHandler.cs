using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Events;

/// <summary>Reacts to a domain event after the change that raised it has been committed.</summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
