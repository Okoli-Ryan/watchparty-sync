using Microsoft.AspNetCore.Identity;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Domain.Identity;

namespace WatchParty.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's hasher (PBKDF2 with a versioned format that upgrades itself),
/// used on its own: the domain owns the User aggregate, so the full Identity user store is not needed.
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public PasswordCheck Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(null!, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}
