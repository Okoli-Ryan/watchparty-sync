using WatchParty.Domain.Identity;

namespace WatchParty.Application.Abstractions.Security;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues the bearer token a signed-in client sends with every request.</summary>
public interface ITokenIssuer
{
    AccessToken Issue(User user);
}
