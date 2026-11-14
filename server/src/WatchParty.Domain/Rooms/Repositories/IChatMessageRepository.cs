namespace WatchParty.Domain.Rooms.Repositories;

/// <summary>Messages are append-only; reading the transcript goes through a query service.</summary>
public interface IChatMessageRepository
{
    void Add(ChatMessage message);
}
