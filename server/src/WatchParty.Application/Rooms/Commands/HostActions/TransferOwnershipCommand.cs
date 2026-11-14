using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.HostActions;

public sealed record TransferOwnershipCommand(UserId ActorId, RoomId RoomId, UserId TargetId);
