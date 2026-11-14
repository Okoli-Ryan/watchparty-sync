using System.Net;
using System.Net.Http.Json;
using WatchParty.Api.Contracts;
using WatchParty.Application.Rooms.Queries;

namespace WatchParty.Api.IntegrationTests.Infrastructure;

/// <summary>The calls most tests need, so each test reads as the scenario it checks.</summary>
public static class RoomRequests
{
    public static readonly string PublicChatKey = Convert.ToBase64String(new byte[32]);

    public static object PublicRoom(string name = "Movie night") => new
    {
        name,
        pageUrl = "https://videos.example.com/watch/1",
        selector = "video#player",
        frameOrigin = "",
        currentTime = 0,
        visibility = "public",
        chatKey = PublicChatKey,
    };

    public static async Task<Guid> CreatePublicRoomAsync(this SignedInClient host, string name = "Movie night")
    {
        var response = await host.Http.PostAsJsonAsync("/api/v1/rooms", PublicRoom(name));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return created!.Id;
    }

    public static async Task<string> JoinAsync(this SignedInClient member, Guid roomId)
    {
        var response = await member.Http.PutAsync($"/api/v1/rooms/{roomId}/members/me", content: null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var joined = await response.Content.ReadFromJsonAsync<JoinResponse>();
        return joined!.Role;
    }

    public static async Task<RoomDto> GetRoomAsync(this SignedInClient member, Guid roomId)
    {
        var room = await member.Http.GetFromJsonAsync<RoomDto>($"/api/v1/rooms/{roomId}");
        return room!;
    }
}
