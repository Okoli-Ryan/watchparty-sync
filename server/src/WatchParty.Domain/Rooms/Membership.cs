using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// One person's presence in one room: a browser with the extension attached to the
/// room's video. Its own aggregate, not a child of <see cref="Room"/>, because it is
/// written every few seconds; folding heartbeats into the room would make every
/// playback write fight them for the room's concurrency token.
/// </summary>
public sealed class Membership : AggregateRoot<MembershipId>
{
    /// <summary>A member whose last heartbeat is older than this is treated as gone.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private Membership()
    {
    }
#pragma warning restore CS8618

    private Membership(MembershipId id, DisplayName displayName, MemberRole role, DateTimeOffset joinedAt, DateTimeOffset lastSeen)
    {
        RoomId = id.RoomId;
        UserId = id.UserId;
        DisplayName = displayName;
        Role = role;
        JoinedAt = joinedAt;
        LastSeen = lastSeen;
    }

    public override MembershipId Id => new(RoomId, UserId);

    public RoomId RoomId { get; private set; }

    public UserId UserId { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public MemberRole Role { get; private set; }

    /// <summary>Set on first join only; decides who inherits a room when the host leaves.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Server time of the latest heartbeat. Always the server's clock, never a client's.</summary>
    public DateTimeOffset LastSeen { get; private set; }

    public static Membership Join(RoomId roomId, UserId userId, DisplayName displayName, MemberRole role, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var now = clock.GetUtcNow();
        var membership = new Membership(new MembershipId(roomId, userId), displayName, role, now, now);
        membership.Raise(new MemberJoined(roomId, userId));
        return membership;
    }

    /// <summary>Coming back to a room: refreshes presence but keeps the original join time.</summary>
    public void Rejoin(DisplayName displayName, MemberRole role, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        DisplayName = displayName;
        Role = role;
        LastSeen = clock.GetUtcNow();
        Raise(new MemberJoined(RoomId, UserId));
    }

    public void Beat(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        LastSeen = clock.GetUtcNow();
        Raise(new MemberHeartbeatRecorded(RoomId, UserId));
    }

    public void AssumeRole(MemberRole role)
    {
        if (Role == role)
        {
            return;
        }

        Role = role;
        Raise(new MemberRoleChanged(RoomId, UserId, role));
    }

    /// <summary>Call before removing the membership, so the departure is announced.</summary>
    public void Leave(bool pruned) => Raise(new MemberLeft(RoomId, UserId, pruned));

    public bool IsPresentAt(DateTimeOffset now) => now - LastSeen <= StaleAfter;
}
