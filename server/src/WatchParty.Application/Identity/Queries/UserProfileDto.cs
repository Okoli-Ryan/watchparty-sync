namespace WatchParty.Application.Identity.Queries;

public sealed record UserProfileDto(Guid Uid, string Email, string DisplayName, string Role, long CreatedAt);
