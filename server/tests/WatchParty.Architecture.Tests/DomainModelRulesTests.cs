using System.Reflection;
using NetArchTest.Rules;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Architecture.Tests;

/// <summary>The building-block rules from ASPNET_GUIDELINES.md §2.3 and §12.</summary>
public sealed class DomainModelRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;

    [Fact]
    public void Aggregates_HaveNoPublicSetters()
    {
        var publicSetters = AggregateTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod is { IsPublic: true })
            .Where(property => !IsInitOnly(property))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}");

        publicSetters.ShouldBeEmpty();
    }

    /// <summary>Sealed by default (§9); a class may stay open only if something actually derives from it.</summary>
    [Fact]
    public void DomainClasses_AreSealedUnlessSomethingDerivesFromThem()
    {
        var domainTypes = DomainAssembly.GetTypes();

        var baseTypes = domainTypes
            .Select(type => type.BaseType)
            .Where(baseType => baseType is not null)
            .Select(baseType => baseType!.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType)
            .ToHashSet();

        var unsealedLeafClasses = domainTypes
            .Where(type => type.IsClass && !type.IsAbstract && !type.IsSealed)
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => !baseTypes.Contains(type))
            .Select(type => type.FullName);

        unsealedLeafClasses.ShouldBeEmpty();
    }

    [Fact]
    public void RepositoryInterfaces_ExistOnlyForAggregateRoots()
    {
        var aggregateNames = AggregateTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var repositoriesWithoutAggregate = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .Select(type => type.Name[1..^"Repository".Length])
            .Where(aggregateName => !aggregateNames.Contains(aggregateName));

        repositoriesWithoutAggregate.ShouldBeEmpty();
    }

    [Fact]
    public void CommandHandlers_AreNamedHandlerOrHandlers()
    {
        var handlerTypes = ApplicationAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition)
            .Where(type => type.GetInterfaces().Any(IsCommandHandler));

        var badlyNamed = handlerTypes
            .Where(type => !type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Where(type => !type.Name.EndsWith("Handlers", StringComparison.Ordinal))
            .Select(type => type.Name);

        badlyNamed.ShouldBeEmpty();
    }

    [Fact]
    public void Commands_AreSealedRecords()
    {
        var commandTypes = ApplicationAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal));

        var notSealedRecords = commandTypes
            .Where(type => !type.IsSealed || type.GetMethod("<Clone>$") is null)
            .Select(type => type.Name);

        notSealedRecords.ShouldBeEmpty();
    }

    private static bool IsCommandHandler(Type candidate) =>
        candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ICommandHandler<,>);

    /// <summary>Reflection, not NetArchTest: its interface check misses one inherited through a generic base.</summary>
    private static IEnumerable<Type> AggregateTypes() =>
        DomainAssembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(IAggregateRoot).IsAssignableFrom(type));

    private static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod!.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
}
