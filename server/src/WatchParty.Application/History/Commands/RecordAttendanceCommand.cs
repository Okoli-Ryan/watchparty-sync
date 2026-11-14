using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands;

/// <summary>Raised internally when someone joins a room; not exposed over HTTP.</summary>
public sealed record RecordAttendanceCommand(UserId ActorId, RoomId RoomId);
