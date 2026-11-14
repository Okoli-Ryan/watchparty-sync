using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WatchParty.Infrastructure.Security;

/// <summary>
/// Signing settings for the API's own access tokens. <see cref="SigningKey"/> is a secret
/// (user-secrets locally, an environment variable or secret store in production).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "watchparty-api";

    [Required]
    public string Audience { get; set; } = "watchparty-clients";

    /// <summary>At least 32 characters: HMAC-SHA256 needs a 256-bit key.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Long enough to outlast a watch party, since there is no refresh token yet. The
    /// account is re-checked on sensitive actions, so a deleted admin cannot use one.
    /// </summary>
    [Range(typeof(TimeSpan), "00:05:00", "30.00:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name",
        RoleClaimType = "role",
    };
}
