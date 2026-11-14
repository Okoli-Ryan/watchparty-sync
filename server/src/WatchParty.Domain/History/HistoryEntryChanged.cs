using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.History;

public sealed record HistoryEntryChanged(UserId UserId, RoomId RoomId) : IDomainEvent;
