using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Identity;

public sealed record UserRegistered(UserId UserId, Role Role) : IDomainEvent;
