using Microsoft.Extensions.Time.Testing;
using WatchParty.Application.Rooms.Commands.Presence;
using WatchParty.Application.Rooms.Services;
using WatchParty.Application.UnitTests.Fakes;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.UnitTests.Rooms;

public sealed class PresenceHandlersTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 20, 0, 0, TimeSpan.Zero));
    private readonly InMemoryStore _store = new();
    private readonly PresenceHandlers _handlers;

    public PresenceHandlersTests()
    {
        var succession = new RoomSuccession(_store, _store, _store, _clock);
        _handlers = new PresenceHandlers(_store, _store, _store, succession, _store, _clock);
    }

    [Fact]
    public async Task Join_AsViewer_CreatesAPresentMembership()
    {
        var host = AddUser("host@example.com");
        var viewer = AddUser("viewer@example.com");
        var room = AddRoom(host);

        var result = await _handlers.HandleAsync(new JoinRoomCommand(viewer.Id, room.Id), CancellationToken.None);

        result.Value.Role.ShouldBe(MemberRole.Viewer);
        var membership = _store.Memberships[new MembershipId(room.Id, viewer.Id)];
        membership.IsPresentAt(_clock.GetUtcNow()).ShouldBeTrue();
    }

    [Fact]
    public async Task Join_EndedRoom_IsRejected()
    {
        var host = AddUser("host@example.com");
        var room = AddRoom(host);
        room.Deactivate();

        var result = await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);

        result.Error.ShouldBe(RoomErrors.Inactive);
    }

    [Fact]
    public async Task Join_ByPrimaryOwnerAfterHandoff_ReclaimsTheRoom()
    {
        var host = AddUser("host@example.com");
        var standIn = AddUser("stand-in@example.com");
        var room = AddRoom(host);
        room.SucceedTo(standIn.Id, standIn.DisplayName);

        var result = await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);

        result.Value.Role.ShouldBe(MemberRole.Owner);
        room.OwnerId.ShouldBe(host.Id);
    }

    [Fact]
    public async Task Heartbeat_AfterHostGoesQuiet_HandsTheRoomToTheEarliestViewer()
    {
        var host = AddUser("host@example.com");
        var viewer = AddUser("viewer@example.com");
        var room = AddRoom(host);
        await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);
        await _handlers.HandleAsync(new JoinRoomCommand(viewer.Id, room.Id), CancellationToken.None);

        // The host's browser crashes: only the viewer keeps heartbeating.
        _clock.Advance(Membership.StaleAfter + TimeSpan.FromSeconds(1));
        var result = await _handlers.HandleAsync(new HeartbeatCommand(viewer.Id, room.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        room.OwnerId.ShouldBe(viewer.Id);
        room.PrimaryOwnerId.ShouldBe(host.Id);
        _store.Memberships.ContainsKey(new MembershipId(room.Id, host.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Heartbeat_AfterBeingPruned_RejoinsInsteadOfFailing()
    {
        var host = AddUser("host@example.com");
        var room = AddRoom(host);

        var result = await _handlers.HandleAsync(new HeartbeatCommand(host.Id, room.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _store.Memberships.ShouldContainKey(new MembershipId(room.Id, host.Id));
    }

    [Fact]
    public async Task Leave_ByHost_HandsOverImmediately()
    {
        var host = AddUser("host@example.com");
        var viewer = AddUser("viewer@example.com");
        var room = AddRoom(host);
        await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);
        await _handlers.HandleAsync(new JoinRoomCommand(viewer.Id, room.Id), CancellationToken.None);

        await _handlers.HandleAsync(new LeaveRoomCommand(host.Id, room.Id), CancellationToken.None);

        room.OwnerId.ShouldBe(viewer.Id);
        room.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Leave_ByLastMember_EndsTheRoom()
    {
        var host = AddUser("host@example.com");
        var room = AddRoom(host);
        await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);

        await _handlers.HandleAsync(new LeaveRoomCommand(host.Id, room.Id), CancellationToken.None);

        room.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Leave_WhenNotAMember_IsHarmless()
    {
        var host = AddUser("host@example.com");
        var stranger = AddUser("stranger@example.com");
        var room = AddRoom(host);
        await _handlers.HandleAsync(new JoinRoomCommand(host.Id, room.Id), CancellationToken.None);

        var result = await _handlers.HandleAsync(new LeaveRoomCommand(stranger.Id, room.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        room.IsActive.ShouldBeTrue();
        room.OwnerId.ShouldBe(host.Id);
    }

    private User AddUser(string email)
    {
        var address = EmailAddress.Create(email).Value;
        var displayName = DisplayName.Create(email.Split('@')[0]).Value;
        var user = User.Register(address, displayName, Role.User, passwordHash: "not-a-real-hash", _clock);
        _store.Add(user);
        return user;
    }

    private Room AddRoom(User owner)
    {
        var room = Room.Create(
            RoomName.Create("Movie night").Value,
            VideoSource.Create("https://videos.example.com/watch/1", "video", "").Value,
            ChatProtection.Public(Convert.ToBase64String(new byte[32])).Value,
            owner.Id,
            owner.DisplayName,
            currentTime: 0,
            _clock).Value;

        _store.Add(room);
        return room;
    }
}
