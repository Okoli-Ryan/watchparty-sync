using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WatchParty.Api.Auth;

namespace WatchParty.Api.Realtime;

/// <summary>
/// Push channel for the extension and the web dashboard. Clients only subscribe here;
/// every change goes through the REST API, which keeps validation and authorisation in
/// one place. Messages sent to clients: <c>room</c>, <c>members</c>, <c>message</c>, <c>history</c>.
/// </summary>
[Authorize]
public sealed class RealtimeHub : Hub
{
    public const string Path = "/hubs/realtime";

    /// <summary>Returns false for a topic that does not exist, rather than failing the connection.</summary>
    public async Task<bool> Subscribe(string topic)
    {
        var group = RealtimeTopics.ResolveGroup(topic, Context.User!.UserId());
        if (group is null)
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        return true;
    }

    public async Task Unsubscribe(string topic)
    {
        var group = RealtimeTopics.ResolveGroup(topic, Context.User!.UserId());
        if (group is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        }
    }
}
