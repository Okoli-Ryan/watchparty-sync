using Microsoft.Extensions.Time.Testing;
using WatchParty.Domain.History;
using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.UnitTests.Builders;

namespace WatchParty.Domain.UnitTests.History;

public sealed class HistoryEntryTests
{
    private readonly FakeTimeProvider _clock = RoomBuilder.Clock();

    [Fact]
    public void MarkReadUpTo_OnlyMovesForward()
    {
        var entry = NewEntry();

        entry.MarkReadUpTo(2_000).Value.ShouldBeTrue();
        entry.MarkReadUpTo(1_000).Value.ShouldBeFalse();
        entry.MarkReadUpTo(2_000).Value.ShouldBeFalse();

        entry.LastReadAt.ShouldBe(2_000);
    }

    [Fact]
    public void MarkReadUpTo_Negative_IsRejected()
    {
        NewEntry().MarkReadUpTo(-1).Error.ShouldBe(HistoryErrors.InvalidReadPosition);
    }

    [Fact]
    public void RecordAttendance_KeepsFavouriteAndReadPosition()
    {
        var entry = NewEntry();
        entry.MarkFavourite(true);
        entry.MarkReadUpTo(5_000);

        entry.RecordAttendance(entry.Room, _clock);

        entry.IsFavourite.ShouldBeTrue();
        entry.LastReadAt.ShouldBe(5_000);
        entry.LastAttendedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public void Track_WithoutAttendance_HasNoAttendanceTime()
    {
        NewEntry().LastAttendedAt.ShouldBeNull();
    }

    private HistoryEntry NewEntry()
    {
        var room = new AttendedRoom("Movie night", "https://example.com/watch", _clock.GetUtcNow());
        return HistoryEntry.Track(UserId.New(), RoomId.New(), room);
    }
}
