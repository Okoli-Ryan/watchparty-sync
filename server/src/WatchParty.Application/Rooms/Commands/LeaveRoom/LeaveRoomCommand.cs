using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.LeaveRoom;

public sealed record LeaveRoomCommand(UserId ActorId, RoomId RoomId);
