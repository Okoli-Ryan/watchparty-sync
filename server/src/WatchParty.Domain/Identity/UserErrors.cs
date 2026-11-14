using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Identity;

public static class UserErrors
{
    public static readonly Error InvalidEmail = Error.Validation("User.InvalidEmail", "That is not a valid e-mail address.");

    public static readonly Error EmailTaken = Error.Conflict("User.EmailTaken", "An account with that e-mail address already exists.");

    public static readonly Error NotFound = Error.NotFound("User.NotFound", "No such user.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("User.InvalidCredentials", "Incorrect e-mail address or password.");
}
