using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Application.Abstractions.Security;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.CreateUser;

/// <summary>The steps shared by the admin flow and the console's first-admin seed.</summary>
internal sealed class UserRegistration(IUserRepository users, IPasswordHasher hasher, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<User>> RegisterAsync(string rawEmail, string password, string? rawDisplayName, Role role, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(rawEmail);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var displayName = DisplayName.Create(string.IsNullOrWhiteSpace(rawDisplayName) ? email.Value.Value : rawDisplayName);
        if (displayName.IsFailure)
        {
            return displayName.Error!;
        }

        if (await users.GetByEmailAsync(email.Value, cancellationToken) is not null)
        {
            return UserErrors.EmailTaken;
        }

        var user = User.Register(email.Value, displayName.Value, role, hasher.Hash(password), clock);
        users.Add(user);
        var committed = await unitOfWork.CommitAsync(cancellationToken);

        // Two admins creating the same address at once: the unique index catches the loser.
        if (committed.Error == ConcurrencyErrors.Duplicate)
        {
            return UserErrors.EmailTaken;
        }

        return committed.IsFailure ? committed.Error! : user;
    }
}
