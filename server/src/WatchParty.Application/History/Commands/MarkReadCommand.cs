using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands;

/// <param name="ActorId">Whose read position.</param>
/// <param name="RoomId">Which room's chat.</param>
/// <param name="At">Epoch ms of the newest message the user has seen.</param>
public sealed record MarkReadCommand(UserId ActorId, RoomId RoomId, long At);
