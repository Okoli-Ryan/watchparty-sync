using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.SignUp;

/// <summary>Creates an ordinary user and signs them straight in, so sign-up is one step for the client.</summary>
internal sealed class SignUpHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SignUpCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> HandleAsync(SignUpCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(command.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var displayName = DisplayName.Create(command.DisplayName);
        if (displayName.IsFailure)
        {
            return displayName.Error!;
        }

        if (await users.GetByEmailAsync(email.Value, cancellationToken) is not null)
        {
            return UserErrors.EmailTaken;
        }

        var user = User.Register(email.Value, displayName.Value, Role.User, hasher.Hash(command.Password), clock);
        users.Add(user);
        var committed = await unitOfWork.CommitAsync(cancellationToken);

        // Two sign-ups for the same address at once: the unique index catches the loser.
        if (committed.Error == ConcurrencyErrors.Duplicate)
        {
            return UserErrors.EmailTaken;
        }

        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        var token = tokens.Issue(user);
        return new LoginResult(token.Token, token.ExpiresAt.ToUnixTimeMilliseconds(), user.ToProfile());
    }
}
