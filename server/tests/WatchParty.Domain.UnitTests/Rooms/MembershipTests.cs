using Microsoft.Extensions.Time.Testing;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.UnitTests.Builders;

namespace WatchParty.Domain.UnitTests.Rooms;

public sealed class MembershipTests
{
    private readonly FakeTimeProvider _clock = RoomBuilder.Clock();

    [Fact]
    public void IsPresentAt_ExactlyAtTheStaleLimit_IsStillPresent()
    {
        var membership = NewMembership();

        _clock.Advance(Membership.StaleAfter);

        membership.IsPresentAt(_clock.GetUtcNow()).ShouldBeTrue();
    }

    [Fact]
    public void IsPresentAt_PastTheStaleLimit_IsGone()
    {
        var membership = NewMembership();

        _clock.Advance(Membership.StaleAfter + TimeSpan.FromMilliseconds(1));

        membership.IsPresentAt(_clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Rejoin_RefreshesPresenceButKeepsTheOriginalJoinTime()
    {
        var membership = NewMembership();
        var originallyJoinedAt = membership.JoinedAt;
        _clock.Advance(TimeSpan.FromMinutes(30));

        membership.Rejoin(DisplayName.Create("Renamed").Value, MemberRole.Owner, _clock);

        membership.JoinedAt.ShouldBe(originallyJoinedAt);
        membership.LastSeen.ShouldBe(_clock.GetUtcNow());
        membership.Role.ShouldBe(MemberRole.Owner);
    }

    private Membership NewMembership() =>
        Membership.Join(RoomId.New(), UserId.New(), DisplayName.Create("Viewer").Value, MemberRole.Viewer, _clock);
}
