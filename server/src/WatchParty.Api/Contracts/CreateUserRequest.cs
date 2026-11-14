using WatchParty.Domain.Identity;

namespace WatchParty.Api.Contracts;

public sealed record CreateUserRequest(string Email, string Password, string? DisplayName, Role Role);
