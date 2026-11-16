using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Heartbeat;

public sealed record HeartbeatCommand(UserId ActorId, RoomId RoomId);
