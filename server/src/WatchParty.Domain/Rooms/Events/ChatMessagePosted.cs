using WatchParty.Domain.SharedKernel;
using WatchParty.Domain.Rooms;

namespace WatchParty.Domain.Rooms.Events;

public sealed record ChatMessagePosted(RoomId RoomId, ChatMessageId MessageId) : IDomainEvent;
