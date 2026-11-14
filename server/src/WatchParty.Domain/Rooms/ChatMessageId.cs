namespace WatchParty.Domain.Rooms;

/// <summary>Version 7 GUIDs sort by creation time, which keeps a room's transcript ordered.</summary>
public readonly record struct ChatMessageId(Guid Value)
{
    public static ChatMessageId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
