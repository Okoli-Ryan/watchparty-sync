using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands;

public sealed record RemoveFromHistoryCommand(UserId ActorId, RoomId RoomId);
