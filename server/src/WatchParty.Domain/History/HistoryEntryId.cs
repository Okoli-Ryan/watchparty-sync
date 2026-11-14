using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.History;

public readonly record struct HistoryEntryId(UserId UserId, RoomId RoomId);
