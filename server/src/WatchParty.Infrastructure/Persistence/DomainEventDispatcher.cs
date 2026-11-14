using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence;

/// <summary>
/// Hands a committed event to every handler registered for its type. A failing handler
/// is logged and does not stop the others or fail the request: the change that raised
/// the event is already durable, and these are after-commit side effects.
/// </summary>
internal sealed partial class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
{
    private static readonly ConcurrentDictionary<Type, Func<object, IDomainEvent, CancellationToken, Task>> Invokers = new();

    private static readonly MethodInfo InvokeOpen =
        typeof(DomainEventDispatcher).GetMethod(nameof(InvokeTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var invoke = Invokers.GetOrAdd(eventType, type =>
            InvokeOpen.MakeGenericMethod(type).CreateDelegate<Func<object, IDomainEvent, CancellationToken, Task>>());

        foreach (var handler in services.GetServices(handlerType))
        {
            try
            {
                await invoke(handler!, domainEvent, cancellationToken);
            }
#pragma warning disable CA1031 // Deliberately broad: see the class summary. Logged, not swallowed.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogHandlerFailed(exception, handler!.GetType().Name, eventType.Name);
            }
        }
    }

    private static Task InvokeTyped<TEvent>(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent =>
        ((IDomainEventHandler<TEvent>)handler).HandleAsync((TEvent)domainEvent, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Handler} failed handling {Event}")]
    private partial void LogHandlerFailed(Exception exception, string handler, string @event);
}
