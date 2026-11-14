using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.CreateUser;

internal sealed class CreateUserHandler(IUserRepository users, UserRegistration registration)
    : ICommandHandler<CreateUserCommand, Result<UserProfileDto>>
{
    public static readonly Error NotAdmin = Error.Forbidden("User.NotAdmin", "Only administrators can create accounts.");

    public async Task<Result<UserProfileDto>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // The endpoint's policy checks the token's role claim; this checks the account as
        // it is NOW, so a demoted or deleted admin's still-valid token cannot create users.
        var actor = await users.GetByIdAsync(command.ActorId, cancellationToken);
        if (actor is not { IsAdmin: true })
        {
            return NotAdmin;
        }

        var user = await registration.RegisterAsync(command.Email, command.Password, command.DisplayName, command.Role, cancellationToken);
        return user.IsFailure ? user.Error! : user.Value.ToProfile();
    }
}
