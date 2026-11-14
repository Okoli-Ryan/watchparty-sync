using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.CreateUser;

/// <param name="ActorId">The administrator making the account.</param>
/// <param name="Email">The new account's e-mail address.</param>
/// <param name="Password">The temporary password the administrator chose.</param>
/// <param name="DisplayName">Optional; blank falls back to the e-mail address.</param>
/// <param name="Role">The new account's role.</param>
public sealed record CreateUserCommand(UserId ActorId, string Email, string Password, string? DisplayName, Role Role);
