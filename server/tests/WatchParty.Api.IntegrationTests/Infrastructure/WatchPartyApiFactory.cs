using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Application.Identity.CreateUser;
using WatchParty.Application.Identity.Queries;
using WatchParty.Application.Identity.SeedAdmin;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API over its own Postgres database, built by the same EF migrations that
/// production runs against Neon. The server clock is a fake, so tests can let
/// heartbeats lapse without waiting.
/// </summary>
public sealed class WatchPartyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "correct horse battery staple";

    private string _connectionString = string.Empty;

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Runs before any test in the class touches the server.</summary>
    public async Task InitializeAsync() => _connectionString = await PostgresServer.CreateDatabaseAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: these must be visible while services
        // are registered, which is before app-configuration callbacks are applied.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:WatchParty", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-0123456789");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>Creates an account straight through the application layer and returns a client signed in as it.</summary>
    public async Task<SignedInClient> SignInAsNewUserAsync(string displayName, Role role = Role.User)
    {
        var email = $"{displayName.ToLowerInvariant().Replace(' ', '.')}.{Guid.NewGuid():N}@example.com";
        var profile = await CreateAccountAsync(email, displayName, role);

        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(new UserId(profile.Uid), CancellationToken.None);
        var token = scope.ServiceProvider.GetRequiredService<ITokenIssuer>().Issue(user!);

        var httpClient = CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return new SignedInClient(httpClient, profile, token.Token);
    }

    public async Task<UserProfileDto> CreateAccountAsync(string email, string displayName, Role role)
    {
        await using var scope = Services.CreateAsyncScope();
        var seedAdmin = scope.ServiceProvider.GetRequiredService<ICommandHandler<SeedAdminCommand, Result<UserProfileDto>>>();
        var createUser = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateUserCommand, Result<UserProfileDto>>>();

        if (role == Role.Admin)
        {
            var admin = await seedAdmin.HandleAsync(new SeedAdminCommand(email, AdminPassword, displayName), CancellationToken.None);
            return admin.Value;
        }

        // Ordinary accounts are made the way production makes them: by an admin.
        var creator = await seedAdmin.HandleAsync(
            new SeedAdminCommand($"creator.{Guid.NewGuid():N}@example.com", AdminPassword, "Creator"),
            CancellationToken.None);

        var command = new CreateUserCommand(new UserId(creator.Value.Uid), email, AdminPassword, displayName, role);
        var created = await createUser.HandleAsync(command, CancellationToken.None);
        return created.Value;
    }
}
