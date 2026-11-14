namespace WatchParty.Api.Contracts;

public sealed record SignUpRequest(string Email, string DisplayName, string Password);
