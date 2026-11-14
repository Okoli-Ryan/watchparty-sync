using System.Net;
using System.Net.Http.Json;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.Rooms.Queries;

namespace WatchParty.Api.IntegrationTests;

public sealed class RoomApiTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    [Fact]
    public async Task CreateRoom_ThenGet_ReturnsWhatTheExtensionExpects()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");

        var roomId = await host.CreatePublicRoomAsync("Friday film");
        var room = await host.GetRoomAsync(roomId);

        room.Name.ShouldBe("Friday film");
        room.Visibility.ShouldBe("public");
        room.ChatKey.ShouldBe(RoomRequests.PublicChatKey);
        room.OwnerUid.ShouldBe(host.UserId);
        room.PrimaryOwnerUid.ShouldBe(host.UserId);
        room.IsActive.ShouldBeTrue();
        room.Playback.IsPlaying.ShouldBeFalse();
        room.Playback.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task CreateRoom_PrivateWithAStoredKey_IsRejected()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");

        var response = await host.Http.PostAsJsonAsync("/api/v1/rooms", new
        {
            pageUrl = "https://videos.example.com/watch/1",
            selector = "video",
            currentTime = 0,
            visibility = "private",
            chatKey = RoomRequests.PublicChatKey,
            chatSalt = Convert.ToBase64String(new byte[16]),
            chatCheck = new { iv = Convert.ToBase64String(new byte[12]), ct = Convert.ToBase64String(new byte[32]) },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordPlayback_ByHost_IsStoredWithServerTime()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var response = await host.Http.PutAsJsonAsync(
            $"/api/v1/rooms/{roomId}/playback",
            new { isPlaying = true, currentTime = 61.5, rate = 1.25 });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var room = await host.GetRoomAsync(roomId);
        room.Playback.IsPlaying.ShouldBeTrue();
        room.Playback.CurrentTime.ShouldBe(61.5);
        room.Playback.Rate.ShouldBe(1.25);
        room.Playback.UpdatedAt.ShouldBe(factory.Clock.GetUtcNow().ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task RecordPlayback_ByViewer_IsForbidden()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        var roomId = await host.CreatePublicRoomAsync();

        var response = await viewer.Http.PutAsJsonAsync(
            $"/api/v1/rooms/{roomId}/playback",
            new { isPlaying = true, currentTime = 1, rate = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordPlayback_WithImpossibleRate_IsBadRequest()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var response = await host.Http.PutAsJsonAsync(
            $"/api/v1/rooms/{roomId}/playback",
            new { isPlaying = true, currentTime = 1, rate = 500 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Touch_WithPosition_SetsHostPositionWithoutChangingPlayback()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var response = await host.Http.PostAsJsonAsync(
            $"/api/v1/rooms/{roomId}/touch",
            new { position = new { currentTime = 300, isPlaying = true } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var room = await host.GetRoomAsync(roomId);
        room.HostPosition.ShouldNotBeNull();
        room.HostPosition.CurrentTime.ShouldBe(300);
        room.Playback.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RepointVideo_RestartsPlaybackAtTheNewPosition()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var response = await host.Http.PutAsJsonAsync($"/api/v1/rooms/{roomId}/video", new
        {
            pageUrl = "https://videos.example.com/watch/2",
            selector = "video.next",
            frameOrigin = "https://player.example.com",
            currentTime = 5,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var room = await host.GetRoomAsync(roomId);
        room.PageUrl.ShouldBe("https://videos.example.com/watch/2");
        room.FrameOrigin.ShouldBe("https://player.example.com");
        room.Playback.CurrentTime.ShouldBe(5);
    }

    [Fact]
    public async Task ListRooms_ReturnsNewestFirst()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var olderId = await host.CreatePublicRoomAsync("Older");
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var newerId = await host.CreatePublicRoomAsync("Newer");

        var rooms = await host.Http.GetFromJsonAsync<List<RoomDto>>("/api/v1/rooms");

        var ids = rooms!.Select(room => room.Id).ToList();
        ids.IndexOf(newerId).ShouldBeLessThan(ids.IndexOf(olderId));
    }

    [Fact]
    public async Task GetRoom_ThatDoesNotExist_IsNotFound()
    {
        using var member = await factory.SignInAsNewUserAsync("Member");

        var response = await member.Http.GetAsync($"/api/v1/rooms/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
