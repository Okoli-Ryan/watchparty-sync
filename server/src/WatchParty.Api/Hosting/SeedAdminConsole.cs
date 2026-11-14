using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Identity.Queries;
using WatchParty.Application.Identity.SeedAdmin;
using WatchParty.Domain.SharedKernel;
using WatchParty.Infrastructure;

namespace WatchParty.Api.Hosting;

/// <summary>
/// <c>dotnet run -- seed-admin --email you@example.com [--name "You"]</c>
///
/// Creates an administrator without the HTTP server, the only way to get the first one.
/// The password comes from <c>WATCHPARTY_ADMIN_PASSWORD</c> or is typed unechoed, so it
/// never lands in shell history or a process listing.
/// </summary>
internal static class SeedAdminConsole
{
    public const string Verb = "seed-admin";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var email = Option(args, "--email");
        if (string.IsNullOrWhiteSpace(email))
        {
            await Console.Error.WriteLineAsync("Usage: seed-admin --email <address> [--name <display name>]");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("WATCHPARTY_ADMIN_PASSWORD") ?? ReadPassword();

        await app.Services.MigrateDatabaseIfConfiguredAsync(CancellationToken.None);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SeedAdminCommand, Result<UserProfileDto>>>();
        var result = await handler.HandleAsync(new SeedAdminCommand(email, password, Option(args, "--name")), CancellationToken.None);

        if (result.IsFailure)
        {
            var error = result.Error!;
            var fieldMessages = error.Fields?.SelectMany(field => field.Value) ?? [];
            var details = string.Join(" ", fieldMessages);
            await Console.Error.WriteLineAsync($"Could not create the admin: {error.Description} {details}");
            return 1;
        }

        await Console.Out.WriteLineAsync($"Created admin {result.Value.Email} ({result.Value.Uid}).");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var optionIndex = Array.IndexOf(args, name);
        var valueIndex = optionIndex + 1;
        var hasValue = optionIndex >= 0 && valueIndex < args.Length;
        return hasValue ? args[valueIndex] : null;
    }

    private static string ReadPassword()
    {
        Console.Write("Password for the new admin: ");
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }
}
