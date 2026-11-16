using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.RepointVideo;

public sealed record RepointVideoCommand(UserId ActorId, RoomId RoomId, string PageUrl, string Selector, string? FrameOrigin, double CurrentTime);
