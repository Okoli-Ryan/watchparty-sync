namespace WatchParty.Domain.History;

/// <summary>
/// What History remembers about a room. Its own type rather than a reference to the Rooms
/// context's aggregate: the application layer copies these fields across the boundary.
/// </summary>
public sealed record AttendedRoom(string Name, string PageUrl, DateTimeOffset? CreatedAt);
