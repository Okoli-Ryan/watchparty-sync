namespace WatchParty.Application.Identity.SeedAdmin;

/// <summary>
/// Creates an administrator from the server console. The only way to get the first
/// admin, since only admins can create accounts. Never exposed over HTTP.
/// </summary>
public sealed record SeedAdminCommand(string Email, string Password, string? DisplayName);
