using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.Presence;

public sealed record JoinRoomCommand(UserId ActorId, RoomId RoomId);
