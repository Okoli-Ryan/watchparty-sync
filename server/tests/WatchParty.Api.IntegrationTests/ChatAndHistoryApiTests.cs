using System.Net;
using System.Net.Http.Json;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.History.Queries;
using WatchParty.Application.Rooms.Queries;

namespace WatchParty.Api.IntegrationTests;

public sealed class ChatAndHistoryApiTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    private static readonly string InitializationVector = Convert.ToBase64String(new byte[12]);

    [Fact]
    public async Task PostMessage_StoresCiphertextUnderTheTokensIdentity()
    {
        using var host = await factory.SignInAsNewUserAsync("Chatty Host");
        var roomId = await host.CreatePublicRoomAsync();
        var ciphertext = Convert.ToBase64String("pretend this is AES-GCM"u8.ToArray());

        var post = await host.Http.PostAsJsonAsync($"/api/v1/rooms/{roomId}/messages", new { iv = InitializationVector, ct = ciphertext });
        post.StatusCode.ShouldBe(HttpStatusCode.Created);

        var messages = await host.Http.GetFromJsonAsync<List<ChatMessageDto>>($"/api/v1/rooms/{roomId}/messages");
        var message = messages.ShouldHaveSingleItem();
        message.Ct.ShouldBe(ciphertext);
        message.SenderUid.ShouldBe(host.UserId);
        message.SenderName.ShouldBe("Chatty Host");
    }

    [Fact]
    public async Task ListMessages_ReturnsOldestFirst()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        foreach (var word in new[] { "first", "second", "third" })
        {
            var ciphertext = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(word));
            await host.Http.PostAsJsonAsync($"/api/v1/rooms/{roomId}/messages", new { iv = InitializationVector, ct = ciphertext });
            factory.Clock.Advance(TimeSpan.FromMilliseconds(5));
        }

        var messages = await host.Http.GetFromJsonAsync<List<ChatMessageDto>>($"/api/v1/rooms/{roomId}/messages");

        var times = messages!.Select(message => message.CreatedAt).ToList();
        times.ShouldBe(times.Order().ToList());
    }

    [Fact]
    public async Task PostMessage_WithMalformedCiphertext_IsBadRequest()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var post = await host.Http.PostAsJsonAsync($"/api/v1/rooms/{roomId}/messages", new { iv = "!!", ct = "not base64" });

        post.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Joining_RecordsAttendance_AndFavouritesStick()
    {
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync("Attended room");
        await host.JoinAsync(roomId);

        await viewer.JoinAsync(roomId);
        var favourite = await viewer.Http.PutAsJsonAsync($"/api/v1/me/history/{roomId}/favourite", new { favourite = true });

        favourite.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var history = await viewer.Http.GetFromJsonAsync<List<HistoryEntryDto>>("/api/v1/me/history");
        var entry = history.ShouldHaveSingleItem();
        entry.RoomName.ShouldBe("Attended room");
        entry.Favourite.ShouldBeTrue();
        entry.LastAttendedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReadPosition_OnlyMovesForward()
    {
        using var reader = await factory.SignInAsNewUserAsync("Reader");
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        await reader.Http.PutAsJsonAsync($"/api/v1/me/history/{roomId}/read-position", new { at = 5_000 });
        await reader.Http.PutAsJsonAsync($"/api/v1/me/history/{roomId}/read-position", new { at = 1_000 });

        var entry = await reader.Http.GetFromJsonAsync<HistoryEntryDto>($"/api/v1/me/history/{roomId}");
        entry!.LastReadAt.ShouldBe(5_000);
    }

    [Fact]
    public async Task ReadPositionOnly_DoesNotAppearInTheHistoryList()
    {
        using var observer = await factory.SignInAsNewUserAsync("Observer");
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        await observer.Http.PutAsJsonAsync($"/api/v1/me/history/{roomId}/read-position", new { at = 1_000 });

        var history = await observer.Http.GetFromJsonAsync<List<HistoryEntryDto>>("/api/v1/me/history");
        history.ShouldBeEmpty();
    }

    [Fact]
    public async Task History_IsPrivateToItsOwner()
    {
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        using var snoop = await factory.SignInAsNewUserAsync("Snoop");
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();
        await host.JoinAsync(roomId);
        await viewer.JoinAsync(roomId);

        var snoopsHistory = await snoop.Http.GetFromJsonAsync<List<HistoryEntryDto>>("/api/v1/me/history");
        var snoopsEntry = await snoop.Http.GetAsync($"/api/v1/me/history/{roomId}");

        snoopsHistory.ShouldBeEmpty();
        snoopsEntry.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Favourite_ForARoomNeverAttended_IsNotFound()
    {
        using var stranger = await factory.SignInAsNewUserAsync("Stranger");

        var response = await stranger.Http.PutAsJsonAsync($"/api/v1/me/history/{Guid.NewGuid()}/favourite", new { favourite = true });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
