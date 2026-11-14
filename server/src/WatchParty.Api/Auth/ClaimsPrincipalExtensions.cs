using System.Security.Claims;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The caller's id from the validated token's <c>sub</c>. Endpoints sit behind the
    /// authenticated-by-default fallback policy, so a missing or malformed claim is a fault.
    /// </summary>
    public static UserId UserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id)
            ? new UserId(id)
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
