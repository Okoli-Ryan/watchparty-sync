using System.Reflection;
using NetArchTest.Rules;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Architecture.Tests;

/// <summary>The dependency rule from ASPNET_GUIDELINES.md §2.1, checked rather than hoped for.</summary>
public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "WatchParty.Domain";
    private const string ApplicationNamespace = "WatchParty.Application";
    private const string InfrastructureNamespace = "WatchParty.Infrastructure";
    private const string ApiNamespace = "WatchParty.Api";

    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_ReferencesNoOtherLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_ReferencesNoFramework()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.Extensions", "Microsoft.EntityFrameworkCore", "Npgsql", "FluentValidation", "System.Net.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_AssemblyReferencesOnlyTheBaseClassLibrary()
    {
        var references = DomainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
            .Where(name => name != "netstandard" && name != "mscorlib");

        references.ShouldBeEmpty();
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace, "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Endpoints_DoNotTouchTheDatabaseDirectly()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That()
            .ResideInNamespace($"{ApiNamespace}.Endpoints")
            .ShouldNot()
            .HaveDependencyOnAny($"{InfrastructureNamespace}.Persistence", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
