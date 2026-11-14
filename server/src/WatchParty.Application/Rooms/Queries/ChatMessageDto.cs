namespace WatchParty.Application.Rooms.Queries;

/// <summary>Still encrypted: decryption happens only in the client.</summary>
public sealed record ChatMessageDto(Guid Id, Guid RoomId, Guid SenderUid, string SenderName, string Iv, string Ct, long CreatedAt);
