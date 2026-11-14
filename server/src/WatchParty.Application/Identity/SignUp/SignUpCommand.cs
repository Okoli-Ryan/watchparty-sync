namespace WatchParty.Application.Identity.SignUp;

/// <summary>Self-service account creation. Always makes an ordinary user, never an administrator.</summary>
public sealed record SignUpCommand(string Email, string DisplayName, string Password);
