using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Identity;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken);

    void Add(User user);

    void Update(User user);
}
