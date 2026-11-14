using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Api.Realtime;

namespace WatchParty.Api.IntegrationTests;

/// <summary>The push that replaces Firestore's onSnapshot.</summary>
public sealed class RealtimeHubTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task HostPlays_SubscribedViewerReceivesTheNewPlayback()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        using var viewer = await factory.SignInAsNewUserAsync("Viewer");
        var roomId = await host.CreatePublicRoomAsync();

        var roomPushes = Channel.CreateUnbounded<JsonElement>();
        await using var connection = Connect(viewer);
        connection.On<JsonElement>("room", room => roomPushes.Writer.TryWrite(room));
        await connection.StartAsync();
        (await connection.InvokeAsync<bool>("Subscribe", $"room:{roomId}")).ShouldBeTrue();

        await host.Http.PutAsJsonAsync($"/api/v1/rooms/{roomId}/playback", new { isPlaying = true, currentTime = 42, rate = 1 });

        var pushed = await ReadUntilAsync(roomPushes, room => room.GetProperty("playback").GetProperty("isPlaying").GetBoolean());
        pushed.GetProperty("playback").GetProperty("currentTime").GetDouble().ShouldBe(42);
    }

    [Fact]
    public async Task MemberJoins_SubscriberReceivesMembersWithServerTime()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = await host.CreatePublicRoomAsync();

        var memberPushes = Channel.CreateUnbounded<JsonElement>();
        await using var connection = Connect(host);
        connection.On<JsonElement>("members", snapshot => memberPushes.Writer.TryWrite(snapshot));
        await connection.StartAsync();
        await connection.InvokeAsync<bool>("Subscribe", $"members:{roomId}");

        await host.JoinAsync(roomId);

        var snapshot = await ReadUntilAsync(memberPushes, members => members.GetProperty("members").GetArrayLength() == 1);
        snapshot.GetProperty("serverTime").GetInt64().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Subscribe_ToAnUnknownTopic_IsRefused()
    {
        using var member = await factory.SignInAsNewUserAsync("Member");
        await using var connection = Connect(member);
        await connection.StartAsync();

        var accepted = await connection.InvokeAsync<bool>("Subscribe", "history:someone-else");

        accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Connect_WithoutAToken_IsRejected()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, RealtimeHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync());
    }

    private HubConnection Connect(SignedInClient member) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, RealtimeHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(member.AccessToken);
            })
            .Build();

    /// <summary>Pushes can arrive for earlier changes too; wait for the one the test is about.</summary>
    private static async Task<JsonElement> ReadUntilAsync(Channel<JsonElement> pushes, Func<JsonElement, bool> isTheOne)
    {
        using var timeout = new CancellationTokenSource(PushTimeout);
        while (true)
        {
            var push = await pushes.Reader.ReadAsync(timeout.Token);
            if (isTheOne(push))
            {
                return push;
            }
        }
    }
}
