using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Identity.Queries;

public interface IUserQueries
{
    Task<UserProfileDto?> GetProfileAsync(UserId userId, CancellationToken cancellationToken);
}
