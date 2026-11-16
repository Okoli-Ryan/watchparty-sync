using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.JoinRoom;

public sealed record JoinRoomCommand(UserId ActorId, RoomId RoomId);
