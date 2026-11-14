namespace WatchParty.Domain.Rooms.Services;

/// <param name="Heir">Who should host now, or null when the current host is still present.</param>
/// <param name="Stale">Members whose heartbeat has lapsed and should be removed.</param>
public sealed record SuccessionPlan(Membership? Heir, IReadOnlyList<Membership> Stale);
