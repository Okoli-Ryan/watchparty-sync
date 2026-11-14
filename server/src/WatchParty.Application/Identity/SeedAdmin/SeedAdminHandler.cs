using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Identity.CreateUser;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.SeedAdmin;

internal sealed class SeedAdminHandler(UserRegistration registration) : ICommandHandler<SeedAdminCommand, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> HandleAsync(SeedAdminCommand command, CancellationToken cancellationToken)
    {
        var user = await registration.RegisterAsync(command.Email, command.Password, command.DisplayName, Role.Admin, cancellationToken);
        return user.IsFailure ? user.Error! : user.Value.ToProfile();
    }
}
