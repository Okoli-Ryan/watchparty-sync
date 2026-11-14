using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.UnitTests.Builders;

namespace WatchParty.Domain.UnitTests.Rooms;

public sealed class RoomTests
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = RoomBuilder.Clock();
    private readonly UserId _hostId = UserId.New();
    private readonly UserId _viewerId = UserId.New();

    [Fact]
    public void Create_WithBlankName_IsUntitled()
    {
        var room = new RoomBuilder().Named("   ").Build(_clock);

        room.Name.Value.ShouldBe(RoomName.Untitled);
    }

    [Fact]
    public void Create_StartsActivePausedAndOwnedByCreatorAsPrimaryOwner()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).StartingAt(42).Build(_clock);

        room.IsActive.ShouldBeTrue();
        room.Playback.IsPlaying.ShouldBeFalse();
        room.Playback.CurrentTime.ShouldBe(42);
        room.Playback.UpdatedAt.ShouldBeNull();
        room.OwnerId.ShouldBe(_hostId);
        room.PrimaryOwnerId.ShouldBe(_hostId);
    }

    [Fact]
    public void RecordPlayback_ByHost_StoresStateStampedWithServerTime()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        _clock.Advance(TimeSpan.FromSeconds(5));

        var result = room.RecordPlayback(_hostId, isPlaying: true, currentTime: 12.5, rate: 1.5, _clock);

        result.IsSuccess.ShouldBeTrue();
        room.Playback.IsPlaying.ShouldBeTrue();
        room.Playback.CurrentTime.ShouldBe(12.5);
        room.Playback.Rate.ShouldBe(1.5);
        room.Playback.UpdatedAt.ShouldBe(_clock.GetUtcNow());
        room.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PlaybackRecorded>();
    }

    [Fact]
    public void RecordPlayback_ByViewer_IsForbiddenAndChangesNothing()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        var result = room.RecordPlayback(_viewerId, isPlaying: true, currentTime: 99, rate: 1, _clock);

        result.Error.ShouldBe(RoomErrors.NotOwner);
        room.Playback.CurrentTime.ShouldBe(0);
        room.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(-1, 1)]
    [InlineData(10, 0)]
    [InlineData(10, 100)]
    public void RecordPlayback_WithImpossibleValues_IsRejected(double currentTime, double rate)
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        var result = room.RecordPlayback(_hostId, isPlaying: true, currentTime, rate, _clock);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void RecordPlayback_OnEndedRoom_IsRejected()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        room.Deactivate();

        var result = room.RecordPlayback(_hostId, isPlaying: true, currentTime: 1, rate: 1, _clock);

        result.Error.ShouldBe(RoomErrors.Inactive);
    }

    [Fact]
    public void Touch_WithPosition_RefreshesHostPositionButNeverPlayback()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var playbackBefore = room.Playback;
        _clock.Advance(TimeSpan.FromSeconds(9));

        var result = room.Touch(_hostId, (CurrentTime: 300, IsPlaying: true), _clock);

        result.IsSuccess.ShouldBeTrue();
        room.HostPosition.ShouldNotBeNull();
        room.HostPosition.CurrentTime.ShouldBe(300);
        room.LastActiveAt.ShouldBe(_clock.GetUtcNow());
        room.Playback.ShouldBeSameAs(playbackBefore);
    }

    [Fact]
    public void RepointVideo_RestartsPausedAtTheNewPositionAndForgetsHostPosition()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        room.Touch(_hostId, (CurrentTime: 500, IsPlaying: true), _clock);
        room.RecordPlayback(_hostId, isPlaying: true, currentTime: 500, rate: 2, _clock);
        var nextEpisode = VideoSource.Create("https://videos.example.com/watch/2", "video", "").Value;

        var result = room.RepointVideo(_hostId, nextEpisode, currentTime: 0, _clock);

        result.IsSuccess.ShouldBeTrue();
        room.Video.ShouldBe(nextEpisode);
        room.Playback.IsPlaying.ShouldBeFalse();
        room.Playback.CurrentTime.ShouldBe(0);
        room.Playback.Rate.ShouldBe(1);
        room.HostPosition.ShouldBeNull();
    }

    [Fact]
    public void TransferTo_MovesBothTheCurrentAndThePersistentClaim()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        var viewerName = DisplayName.Create("Viewer").Value;

        var result = room.TransferTo(_hostId, _viewerId, viewerName);

        result.IsSuccess.ShouldBeTrue();
        room.OwnerId.ShouldBe(_viewerId);
        room.PrimaryOwnerId.ShouldBe(_viewerId);
        var ownershipChanged = room.DomainEvents.OfType<RoomOwnershipChanged>().ShouldHaveSingleItem();
        ownershipChanged.Deliberate.ShouldBeTrue();
    }

    [Fact]
    public void TransferTo_Self_IsRejected()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        var result = room.TransferTo(_hostId, _hostId, DisplayName.Create("Host").Value);

        result.Error.ShouldBe(RoomErrors.CannotTransferToSelf);
    }

    [Fact]
    public void SucceedTo_HandsOverDrivingButKeepsThePersistentClaim()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        room.SucceedTo(_viewerId, DisplayName.Create("Viewer").Value);

        room.OwnerId.ShouldBe(_viewerId);
        room.PrimaryOwnerId.ShouldBe(_hostId);
    }

    [Fact]
    public void Reclaim_ByReturningPrimaryOwner_TakesTheRoomBack()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);
        room.SucceedTo(_viewerId, DisplayName.Create("Viewer").Value);

        var result = room.Reclaim(_hostId, DisplayName.Create("Host").Value);

        result.Value.ShouldBeTrue();
        room.OwnerId.ShouldBe(_hostId);
    }

    [Fact]
    public void Reclaim_WhenAlreadyHosting_ChangesNothing()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        var result = room.Reclaim(_hostId, DisplayName.Create("Host").Value);

        result.Value.ShouldBeFalse();
        room.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Reclaim_BySomeoneElse_IsForbidden()
    {
        var room = new RoomBuilder().OwnedBy(_hostId).Build(_clock);

        var result = room.Reclaim(_viewerId, DisplayName.Create("Viewer").Value);

        result.Error.ShouldBe(RoomErrors.NotPrimaryOwner);
    }

    [Fact]
    public void Deactivate_Twice_AnnouncesItOnce()
    {
        var room = new RoomBuilder().Build(_clock);

        room.Deactivate();
        room.Deactivate();

        room.IsActive.ShouldBeFalse();
        room.DomainEvents.OfType<RoomDeactivated>().Count().ShouldBe(1);
    }
}
