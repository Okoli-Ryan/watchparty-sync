using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.History;

/// <summary>
/// One person's record of one room: when they last attended, whether it is a favourite,
/// and how far they have read its chat. Per person by design (DECISIONS.md #16, #20).
/// </summary>
public sealed class HistoryEntry : AggregateRoot<HistoryEntryId>
{
#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private HistoryEntry()
    {
    }
#pragma warning restore CS8618

    private HistoryEntry(HistoryEntryId id, AttendedRoom room)
    {
        UserId = id.UserId;
        RoomId = id.RoomId;
        Room = room;
    }

    public override HistoryEntryId Id => new(UserId, RoomId);

    public UserId UserId { get; private set; }

    public RoomId RoomId { get; private set; }

    /// <summary>Denormalised from the room so the history list needs no join.</summary>
    public AttendedRoom Room { get; private set; }

    public DateTimeOffset? LastAttendedAt { get; private set; }

    public bool IsFavourite { get; private set; }

    /// <summary>
    /// Epoch ms of the newest message this person has seen, shared by every client they
    /// use. Unread counts are derived from it, so reading anywhere settles them everywhere.
    /// </summary>
    public long? LastReadAt { get; private set; }

    /// <summary>
    /// Start tracking a room. An entry can exist before any attendance: the web dashboard
    /// records read position for rooms it only observes.
    /// </summary>
    public static HistoryEntry Track(UserId userId, RoomId roomId, AttendedRoom room)
    {
        var entry = new HistoryEntry(new HistoryEntryId(userId, roomId), room);
        entry.Raise(new HistoryEntryChanged(userId, roomId));
        return entry;
    }

    /// <summary>"I was here." Keeps an existing favourite flag and read position.</summary>
    public void RecordAttendance(AttendedRoom room, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(clock);
        Room = room;
        LastAttendedAt = clock.GetUtcNow();
        Raise(new HistoryEntryChanged(UserId, RoomId));
    }

    public void MarkFavourite(bool favourite)
    {
        if (IsFavourite == favourite)
        {
            return;
        }

        IsFavourite = favourite;
        Raise(new HistoryEntryChanged(UserId, RoomId));
    }

    /// <summary>
    /// Advance the read position. It only moves forward: a client that is behind (slow
    /// snapshot, older tab) must never un-read messages another client has already read.
    /// Returns false when the position did not move.
    /// </summary>
    public Result<bool> MarkReadUpTo(long epochMs)
    {
        if (epochMs < 0)
        {
            return HistoryErrors.InvalidReadPosition;
        }

        if (LastReadAt is { } current && epochMs <= current)
        {
            return false;
        }

        LastReadAt = epochMs;
        Raise(new HistoryEntryChanged(UserId, RoomId));
        return true;
    }

    /// <summary>Call before removing the entry, so the removal is announced.</summary>
    public void Forget() => Raise(new HistoryEntryRemoved(UserId, RoomId));
}
