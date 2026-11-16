using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.RecordPlayback;

public sealed record RecordPlaybackCommand(UserId ActorId, RoomId RoomId, bool IsPlaying, double CurrentTime, double Rate);
