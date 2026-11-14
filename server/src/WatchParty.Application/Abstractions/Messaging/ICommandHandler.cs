using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Messaging;

/// <summary>
/// A use case that changes state. Handlers orchestrate (load, call the domain, persist)
/// and contain no business rules. Cross-cutting concerns are decorators.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TResult : Result
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
