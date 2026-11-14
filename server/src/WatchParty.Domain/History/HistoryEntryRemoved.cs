using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.History;

public sealed record HistoryEntryRemoved(UserId UserId, RoomId RoomId) : IDomainEvent;
