using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.Identity;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(WatchPartyDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public void Update(User user) => dbContext.AttachIfDetached(user);
}
