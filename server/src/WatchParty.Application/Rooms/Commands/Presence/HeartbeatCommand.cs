using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Presence;

public sealed record HeartbeatCommand(UserId ActorId, RoomId RoomId);
