namespace WatchParty.Domain.Rooms.Services;

/// <summary>
/// Decides who hosts a room once the host has gone quiet, and who has gone stale.
///
/// In the Firebase build every client ran this against its own view of the members,
/// guarded by a calibrated clock and a "snapshot came from cache" check, because a
/// joiner's partial view once made it promote itself. Here it runs once, on the
/// server, against the complete membership and the server's own clock, so neither
/// guard is needed.
/// </summary>
public static class OwnershipSuccession
{
    public static SuccessionPlan Plan(Room room, IReadOnlyCollection<Membership> members, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(members);

        var presentMembers = members
            .Where(member => member.IsPresentAt(now))
            .ToList();

        var staleMembers = members
            .Where(member => !member.IsPresentAt(now))
            .ToList();

        var heir = ChooseHeir(room, presentMembers);
        return new SuccessionPlan(heir, staleMembers);
    }

    /// <summary>Null when no handoff is needed: the host is still here, or nobody is.</summary>
    private static Membership? ChooseHeir(Room room, List<Membership> presentMembers)
    {
        if (!room.IsActive || presentMembers.Count == 0)
        {
            return null;
        }

        var isOwnerPresent = presentMembers.Exists(member => member.UserId == room.OwnerId);
        if (isOwnerPresent)
        {
            return null;
        }

        // The primary owner outranks join order: if they're here, the room goes back
        // to them rather than to whoever arrived first.
        var primaryOwner = presentMembers.Find(member => member.UserId == room.PrimaryOwnerId);
        if (primaryOwner is not null)
        {
            return primaryOwner;
        }

        var earliestJoiner = presentMembers
            .OrderBy(member => member.JoinedAt)
            .ThenBy(member => member.UserId.Value)
            .First();

        return earliestJoiner;
    }
}
