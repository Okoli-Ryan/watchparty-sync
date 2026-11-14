using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Domain.Identity;

namespace WatchParty.Infrastructure.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.Add(settings.AccessTokenLifetime);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.CreateKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("name", user.DisplayName.Value),
                new Claim("role", user.IsAdmin ? "admin" : "user"),
            ]),
        });

        return new AccessToken(token, expires);
    }
}
