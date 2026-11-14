using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Queries;

/// <summary>Read side for rooms, members and chat. Projections only; never loads aggregates.</summary>
public interface IRoomQueries
{
    public const int MaxRoomPage = 50;

    public const int MaxChatPage = 100;

    Task<RoomDto?> GetRoomAsync(RoomId roomId, CancellationToken cancellationToken);

    /// <summary>Newest first, live and ended alike; clients split them by liveness.</summary>
    Task<IReadOnlyList<RoomDto>> ListRecentRoomsAsync(int limit, long? createdBefore, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberDto>> ListMembersAsync(RoomId roomId, CancellationToken cancellationToken);

    /// <summary>The newest <paramref name="limit"/> messages, returned oldest first for reading.</summary>
    Task<IReadOnlyList<ChatMessageDto>> ListRecentMessagesAsync(RoomId roomId, int limit, CancellationToken cancellationToken);

    Task<ChatMessageDto?> GetMessageAsync(ChatMessageId messageId, CancellationToken cancellationToken);
}
