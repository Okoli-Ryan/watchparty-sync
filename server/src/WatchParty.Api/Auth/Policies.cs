namespace WatchParty.Api.Auth;

public static class Policies
{
    public const string LoginRateLimit = "login";

    public const string PerUserRateLimit = "per-user";
}
