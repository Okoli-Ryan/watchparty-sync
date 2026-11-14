using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.Login;

internal sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    IUnitOfWork unitOfWork) : ICommandHandler<LoginCommand, Result<LoginResult>>
{
    /// <summary>
    /// A real hash of a random password nobody knows. Verified against when the e-mail is
    /// unknown, so an unknown account takes as long to reject as a wrong password and
    /// response timing does not reveal which accounts exist. Benign race on first use.
    /// </summary>
    private static string? _decoyHash;

    public async Task<Result<LoginResult>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(command.Email);
        User? user = null;
        if (email.IsSuccess)
        {
            user = await users.GetByEmailAsync(email.Value, cancellationToken);
        }

        var hashToCheck = user?.PasswordHash ?? DecoyHash();
        var check = hasher.Verify(hashToCheck, command.Password);
        if (user is null || check == PasswordCheck.Failed)
        {
            return UserErrors.InvalidCredentials;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.RehashPassword(hasher.Hash(command.Password));
            users.Update(user);
            await unitOfWork.CommitAsync(cancellationToken);
        }

        var token = tokens.Issue(user);
        var expiresAt = token.ExpiresAt.ToUnixTimeMilliseconds();
        return new LoginResult(token.Token, expiresAt, user.ToProfile());
    }

    private string DecoyHash()
    {
        if (_decoyHash is null)
        {
            var unguessablePassword = Guid.NewGuid().ToString("N");
            _decoyHash = hasher.Hash(unguessablePassword);
        }

        return _decoyHash;
    }
}
