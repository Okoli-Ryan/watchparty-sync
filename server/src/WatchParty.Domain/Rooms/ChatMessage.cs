using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// One chat message. Immutable once sent. The body is ciphertext; only the sender
/// and the time are stored in the clear.
/// </summary>
public sealed class ChatMessage : AggregateRoot<ChatMessageId>
{
#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private ChatMessage()
    {
    }
#pragma warning restore CS8618

    private ChatMessage(ChatMessageId id, RoomId roomId, UserId senderId, DisplayName senderName, EncryptedText body, DateTimeOffset sentAt)
    {
        Id = id;
        RoomId = roomId;
        SenderId = senderId;
        SenderName = senderName;
        Body = body;
        SentAt = sentAt;
    }

    public RoomId RoomId { get; private set; }

    public UserId SenderId { get; private set; }

    public DisplayName SenderName { get; private set; }

    public EncryptedText Body { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Chat stays open after a room ends, so posting needs only that the room exists.</summary>
    public static ChatMessage Post(Room room, UserId senderId, DisplayName senderName, EncryptedText body, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(clock);
        var message = new ChatMessage(ChatMessageId.New(), room.Id, senderId, senderName, body, clock.GetUtcNow());
        message.Raise(new ChatMessagePosted(room.Id, message.Id));
        return message;
    }
}
