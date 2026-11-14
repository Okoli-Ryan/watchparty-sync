using WatchParty.Application.Identity.Queries;

namespace WatchParty.Application.Identity.Login;

public sealed record LoginResult(string AccessToken, long ExpiresAt, UserProfileDto User);
