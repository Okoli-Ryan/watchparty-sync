using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.TouchRoom;

/// <param name="ActorId">The host.</param>
/// <param name="RoomId">The room.</param>
/// <param name="CurrentTime">The host's playhead now, or null to refresh liveness only.</param>
/// <param name="IsPlaying">Whether the host is playing; required with <paramref name="CurrentTime"/>.</param>
public sealed record TouchRoomCommand(UserId ActorId, RoomId RoomId, double? CurrentTime, bool? IsPlaying);
