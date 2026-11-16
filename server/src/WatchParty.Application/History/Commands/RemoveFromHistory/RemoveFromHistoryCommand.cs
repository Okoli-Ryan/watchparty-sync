using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.RemoveFromHistory;

public sealed record RemoveFromHistoryCommand(UserId ActorId, RoomId RoomId);
