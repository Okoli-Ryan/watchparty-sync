using WatchParty.Domain.Identity;

namespace WatchParty.Application.Identity.Queries;

public static class UserProfileMapping
{
    public static UserProfileDto ToProfile(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserProfileDto(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName.Value,
            user.IsAdmin ? "admin" : "user",
            user.CreatedAt.ToUnixTimeMilliseconds());
    }
}
