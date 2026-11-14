using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Queries;

internal sealed class UserQueries(WatchPartyDbContext dbContext) : IUserQueries
{
    /// <summary>Projects only profile columns: the password hash never leaves the database.</summary>
    public Task<UserProfileDto?> GetProfileAsync(UserId userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserProfileDto(
                user.Id.Value,
                user.Email.Value,
                user.DisplayName.Value,
                user.Role == Role.Admin ? "admin" : "user",
                user.CreatedAt.ToUnixTimeMilliseconds()))
            .SingleOrDefaultAsync(cancellationToken);
}
