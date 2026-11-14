using Microsoft.Extensions.Time.Testing;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Services;
using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.UnitTests.Builders;

namespace WatchParty.Domain.UnitTests.Rooms;

public sealed class OwnershipSuccessionTests
{
    private readonly FakeTimeProvider _clock = RoomBuilder.Clock();
    private readonly UserId _hostId = UserId.New();

    [Fact]
    public void Plan_WhenHostIsPresent_NamesNoHeir()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var host = Join(room, _hostId);
        var viewer = Join(room, UserId.New());

        var plan = OwnershipSuccession.Plan(room, [host, viewer], _clock.GetUtcNow());

        plan.Heir.ShouldBeNull();
        plan.Stale.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_WhenHostHasGoneQuiet_EarliestPresentJoinerInherits()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var host = Join(room, _hostId);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var firstViewer = Join(room, UserId.New());
        _clock.Advance(TimeSpan.FromSeconds(1));
        var secondViewer = Join(room, UserId.New());

        // The host stops heartbeating; both viewers keep going.
        _clock.Advance(Membership.StaleAfter + TimeSpan.FromSeconds(1));
        firstViewer.Beat(_clock);
        secondViewer.Beat(_clock);

        var plan = OwnershipSuccession.Plan(room, [host, firstViewer, secondViewer], _clock.GetUtcNow());

        plan.Heir.ShouldBe(firstViewer);
        plan.Stale.ShouldHaveSingleItem().ShouldBe(host);
    }

    [Fact]
    public void Plan_WhenPrimaryOwnerIsPresent_TheyOutrankAnEarlierJoiner()
    {
        var primaryOwnerId = UserId.New();
        var room = new RoomBuilder().OwnedBy(primaryOwnerId).Build(_clock);
        var earlyViewerId = UserId.New();
        room.SucceedTo(earlyViewerId, DisplayName.Create("Early").Value);

        var earlyViewer = Join(room, earlyViewerId);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var lateViewer = Join(room, UserId.New());
        _clock.Advance(TimeSpan.FromSeconds(1));
        var returningPrimaryOwner = Join(room, primaryOwnerId);

        // The stand-in host goes quiet.
        _clock.Advance(Membership.StaleAfter + TimeSpan.FromSeconds(1));
        lateViewer.Beat(_clock);
        returningPrimaryOwner.Beat(_clock);

        var plan = OwnershipSuccession.Plan(room, [earlyViewer, lateViewer, returningPrimaryOwner], _clock.GetUtcNow());

        plan.Heir.ShouldBe(returningPrimaryOwner);
    }

    [Fact]
    public void Plan_WhenNobodyIsPresent_NamesNoHeirAndListsEveryoneStale()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var host = Join(room, _hostId);
        var viewer = Join(room, UserId.New());
        _clock.Advance(Membership.StaleAfter + TimeSpan.FromSeconds(1));

        var plan = OwnershipSuccession.Plan(room, [host, viewer], _clock.GetUtcNow());

        plan.Heir.ShouldBeNull();
        plan.Stale.Count.ShouldBe(2);
    }

    [Fact]
    public void Plan_ForEndedRoom_NamesNoHeir()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var viewer = Join(room, UserId.New());
        room.Deactivate();

        var plan = OwnershipSuccession.Plan(room, [viewer], _clock.GetUtcNow());

        plan.Heir.ShouldBeNull();
    }

    private Membership Join(Room room, UserId userId)
    {
        var displayName = DisplayName.Create($"Member {userId.Value.ToString()[..4]}").Value;
        return Membership.Join(room.Id, userId, displayName, MemberRole.Viewer, _clock);
    }
}
