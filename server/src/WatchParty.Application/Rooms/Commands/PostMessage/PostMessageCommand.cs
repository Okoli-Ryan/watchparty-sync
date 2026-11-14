using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.PostMessage;

/// <summary>A chat message, already encrypted by the sender's browser.</summary>
public sealed record PostMessageCommand(UserId ActorId, RoomId RoomId, string Iv, string Ct);
