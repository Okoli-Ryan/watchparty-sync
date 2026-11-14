using WatchParty.Application.Identity.Queries;

namespace WatchParty.Api.IntegrationTests.Infrastructure;

/// <summary>An HTTP client carrying one user's bearer token.</summary>
public sealed record SignedInClient(HttpClient Http, UserProfileDto Profile, string AccessToken) : IDisposable
{
    public Guid UserId => Profile.Uid;

    public void Dispose() => Http.Dispose();
}
