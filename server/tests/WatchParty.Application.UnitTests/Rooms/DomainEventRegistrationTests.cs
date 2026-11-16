using Microsoft.Extensions.DependencyInjection;
using WatchParty.Application.Abstractions.Events;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.UnitTests.Rooms;

public sealed class DomainEventRegistrationTests
{
    /// <summary>Raised by the domain but deliberately not pushed to clients or reacted to.</summary>
    private static readonly Type[] HandledNowhereOnPurpose = [typeof(UserRegistered)];

    [Fact]
    public void EveryDomainEvent_HasAHandlerRegistered_OrIsExemptOnPurpose()
    {
        var services = new ServiceCollection().AddApplication();
        var handled = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(type => type.GetGenericArguments()[0])
            .ToHashSet();

        var unhandled = typeof(IDomainEvent).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type))
            .Where(type => !handled.Contains(type) && !HandledNowhereOnPurpose.Contains(type))
            .Select(type => type.Name)
            .ToList();

        unhandled.ShouldBeEmpty("these domain events reach no handler, so clients are never told about them");
    }
}
