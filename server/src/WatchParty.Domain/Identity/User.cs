using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Identity;

/// <summary>
/// An account. Accounts are only ever created by an administrator; there is no
/// self-service sign-up (the original Firebase build disabled it for the same reason).
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private User()
    {
    }
#pragma warning restore CS8618

    private User(UserId id, EmailAddress email, DisplayName displayName, Role role, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        Role = role;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public EmailAddress Email { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public Role Role { get; private set; }

    /// <summary>Opaque hash produced by the application's password hasher. Never the password.</summary>
    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsAdmin => Role == Role.Admin;

    public static User Register(EmailAddress email, DisplayName displayName, Role role, string passwordHash, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(clock);
        var user = new User(UserId.New(), email, displayName, role, passwordHash, clock.GetUtcNow());
        user.Raise(new UserRegistered(user.Id, role));
        return user;
    }

    /// <summary>Replace the stored hash, e.g. after the hasher reports it should be upgraded.</summary>
    public void RehashPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }
}
