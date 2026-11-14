using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Realtime;

/// <summary>
/// What a client can subscribe to, and the SignalR group behind each. These stand in for
/// the Firestore listeners the extension used to open.
/// </summary>
public static class RealtimeTopics
{
    /// <summary>Every room as it changes: the room list and the dashboard.</summary>
    public const string Rooms = "rooms";

    /// <summary>The caller's own history; mapped to a per-user group, never someone else's.</summary>
    public const string History = "history";

    public static string RoomGroup(RoomId id) => $"room:{id}";

    public static string MembersGroup(RoomId id) => $"members:{id}";

    public static string ChatGroup(RoomId id) => $"chat:{id}";

    public static string HistoryGroup(UserId id) => $"history:{id}";

    /// <summary>Maps a topic a client asked for to the group it may join, or null if it is not one.</summary>
    public static string? ResolveGroup(string? topic, UserId caller)
    {
        switch (topic)
        {
            case Rooms:
                return Rooms;
            case History:
                return HistoryGroup(caller);
            case null:
                return null;
        }

        var separator = topic.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || !Guid.TryParse(topic.AsSpan(separator + 1), out var id))
        {
            return null;
        }

        var roomId = new RoomId(id);
        return topic[..separator] switch
        {
            "room" => RoomGroup(roomId),
            "members" => MembersGroup(roomId),
            "chat" => ChatGroup(roomId),
            _ => null,
        };
    }
}
