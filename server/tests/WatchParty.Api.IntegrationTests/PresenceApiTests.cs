using System.Net;
using System.Net.Http.Json;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.IntegrationTests;

/// <summary>
/// Ownership handoff, the part the Firebase build kept getting wrong. Every client used
/// to decide it from its own partial view; now the server decides, so these scenarios
/// run against one authoritative membership list and one clock.
/// </summary>
public sealed class PresenceApiTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    [Fact]
    public async Task Join_HostIsOwnerAndViewerIsViewer()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        var roomId = await host.CreatePublicRoomAsync();

        (await host.JoinAsync(roomId)).ShouldBe("owner");
        (await viewer.JoinAsync(roomId)).ShouldBe("viewer");

        var snapshot = await host.Http.GetFromJsonAsync<MembersSnapshotDto>($"/api/v1/rooms/{roomId}/members");
        snapshot!.Members.Count.ShouldBe(2);
        snapshot.ServerTime.ShouldBe(factory.Clock.GetUtcNow().ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task HostGoesQuiet_ViewerInherits_ThenHostReturnsAndReclaims()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        var roomId = await host.CreatePublicRoomAsync();
        await host.JoinAsync(roomId);
        await viewer.JoinAsync(roomId);

        // The host's browser vanishes; only the viewer keeps heartbeating.
        factory.Clock.Advance(Membership.StaleAfter + TimeSpan.FromSeconds(1));
        var heartbeat = await viewer.Http.PostAsync($"/api/v1/rooms/{roomId}/members/me/heartbeat", content: null);
        heartbeat.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterHandoff = await viewer.GetRoomAsync(roomId);
        afterHandoff.OwnerUid.ShouldBe(viewer.UserId);
        afterHandoff.PrimaryOwnerUid.ShouldBe(host.UserId);

        // The host comes back and takes the room back.
        (await host.JoinAsync(roomId)).ShouldBe("owner");
        var afterReturn = await host.GetRoomAsync(roomId);
        afterReturn.OwnerUid.ShouldBe(host.UserId);
    }

    [Fact]
    public async Task Transfer_ToAPresentViewer_MovesThePersistentClaim()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        var roomId = await host.CreatePublicRoomAsync();
        await host.JoinAsync(roomId);
        await viewer.JoinAsync(roomId);

        var response = await host.Http.PostAsJsonAsync(
            $"/api/v1/rooms/{roomId}/ownership-transfers",
            new { targetUid = viewer.UserId });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var room = await host.GetRoomAsync(roomId);
        room.OwnerUid.ShouldBe(viewer.UserId);
        room.PrimaryOwnerUid.ShouldBe(viewer.UserId);

        // A deliberate transfer is not undone by the old host rejoining.
        (await host.JoinAsync(roomId)).ShouldBe("viewer");
    }

    [Fact]
    public async Task Transfer_ToSomeoneNotInTheRoom_IsConflict()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var outsider = await factory.SignInAsNewUserAsync("Outsider");
        var roomId = await host.CreatePublicRoomAsync();
        await host.JoinAsync(roomId);

        var response = await host.Http.PostAsJsonAsync(
            $"/api/v1/rooms/{roomId}/ownership-transfers",
            new { targetUid = outsider.UserId });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LastMemberLeaves_RoomEnds_AndCannotBeJoined()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var latecomer = await factory.SignInAsNewUserAsync("Latecomer");
        var roomId = await host.CreatePublicRoomAsync();
        await host.JoinAsync(roomId);

        var leave = await host.Http.DeleteAsync($"/api/v1/rooms/{roomId}/members/me");

        leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.GetRoomAsync(roomId)).IsActive.ShouldBeFalse();
        var join = await latecomer.Http.PutAsync($"/api/v1/rooms/{roomId}/members/me", content: null);
        join.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
