using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Queries;

/// <summary>
/// Read side for rooms, members and chat: untracked projections straight into DTOs,
/// never aggregates loaded just to be displayed (§2.5).
/// </summary>
internal sealed class RoomQueries(WatchPartyDbContext dbContext) : IRoomQueries
{
    public Task<RoomDto?> GetRoomAsync(RoomId roomId, CancellationToken cancellationToken) =>
        dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(ToRoomDto)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RoomDto>> ListRecentRoomsAsync(
        int limit,
        long? createdBefore,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(limit, 1, IRoomQueries.MaxRoomPage);
        var rooms = dbContext.Rooms.AsNoTracking();

        if (createdBefore is { } cursor)
        {
            var cursorTime = DateTimeOffset.FromUnixTimeMilliseconds(cursor);
            rooms = rooms.Where(room => room.CreatedAt < cursorTime);
        }

        return await rooms
            .OrderByDescending(room => room.CreatedAt)
            .Take(pageSize)
            .Select(ToRoomDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(RoomId roomId, CancellationToken cancellationToken) =>
        await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.RoomId == roomId)
            .OrderBy(membership => membership.JoinedAt)
            .Select(ToMemberDto)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatMessageDto>> ListRecentMessagesAsync(
        RoomId roomId,
        int limit,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(limit, 1, IRoomQueries.MaxChatPage);

        var newestFirst = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.RoomId == roomId)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(pageSize)
            .Select(ToMessageDto)
            .ToListAsync(cancellationToken);

        // Newest first to apply the limit; oldest first for reading.
        newestFirst.Reverse();
        return newestFirst;
    }

    public Task<ChatMessageDto?> GetMessageAsync(ChatMessageId messageId, CancellationToken cancellationToken) =>
        dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(ToMessageDto)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Strings and epoch milliseconds, the shapes the extension already reads. EF fetches
    /// the columns; the small conversions (id to Guid, time to epoch ms) run as it
    /// materialises each row.
    /// </summary>
    private static readonly Expression<Func<Room, RoomDto>> ToRoomDto = room => new RoomDto(
        room.Id.Value,
        room.Name.Value,
        room.Video.PageUrl,
        room.Video.Selector,
        room.Video.FrameOrigin,
        room.Chat.Visibility == RoomVisibility.Private ? "private" : "public",
        room.Chat.ChatKey,
        room.Chat.ChatSalt,
        room.Chat.Verifier == null ? null : new EncryptedTextDto(room.Chat.Verifier.Iv, room.Chat.Verifier.Ciphertext),
        room.OwnerId.Value,
        room.OwnerName.Value,
        room.PrimaryOwnerId.Value,
        room.PrimaryOwnerName.Value,
        room.IsActive,
        new PlaybackDto(
            room.Playback.IsPlaying,
            room.Playback.CurrentTime,
            room.Playback.Rate,
            room.Playback.UpdatedAt == null ? null : room.Playback.UpdatedAt.Value.ToUnixTimeMilliseconds(),
            room.Playback.UpdatedBy.Value),
        room.HostPosition == null
            ? null
            : new HostPositionDto(
                room.HostPosition.CurrentTime,
                room.HostPosition.IsPlaying,
                room.HostPosition.UpdatedAt.ToUnixTimeMilliseconds()),
        room.LastActiveAt.ToUnixTimeMilliseconds(),
        room.CreatedAt.ToUnixTimeMilliseconds());

    private static readonly Expression<Func<Membership, MemberDto>> ToMemberDto = membership => new MemberDto(
        membership.UserId.Value,
        membership.DisplayName.Value,
        membership.Role == MemberRole.Owner ? "owner" : "viewer",
        membership.JoinedAt.ToUnixTimeMilliseconds(),
        membership.LastSeen.ToUnixTimeMilliseconds());

    private static readonly Expression<Func<ChatMessage, ChatMessageDto>> ToMessageDto = message => new ChatMessageDto(
        message.Id.Value,
        message.RoomId.Value,
        message.SenderId.Value,
        message.SenderName.Value,
        message.Body.Iv,
        message.Body.Ciphertext,
        message.SentAt.ToUnixTimeMilliseconds());
}
