namespace WatchParty.Application.Abstractions.Security;

public enum PasswordCheck
{
    Failed,
    Success,

    /// <summary>Correct, but hashed with outdated parameters: store a fresh hash.</summary>
    SuccessRehashNeeded,
}

/// <summary>Port over a proven password-hashing implementation. Never roll your own.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string hash, string password);
}
