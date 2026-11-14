using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Presence;

public sealed record LeaveRoomCommand(UserId ActorId, RoomId RoomId);
