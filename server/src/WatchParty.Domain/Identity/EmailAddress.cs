using System.Net.Mail;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Identity;

/// <summary>A normalised (trimmed, lower-cased) e-mail address.</summary>
public sealed record EmailAddress
{
    public const int MaxLength = 254;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static Result<EmailAddress> Create(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length is 0 or > MaxLength || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
        {
            return UserErrors.InvalidEmail;
        }

        return new EmailAddress(value);
    }

    public override string ToString() => Value;
}
