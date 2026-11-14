using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.CreateRoom;

/// <summary>
/// Key material arrives already prepared by the client: a random key for a public room,
/// or a salt and verifier for a private one. The passphrase itself never leaves the browser.
/// </summary>
public sealed record CreateRoomCommand(
    UserId ActorId,
    string? Name,
    string PageUrl,
    string Selector,
    string? FrameOrigin,
    double CurrentTime,
    RoomVisibility Visibility,
    string? ChatKey,
    string? ChatSalt,
    string? ChatCheckIv,
    string? ChatCheckCt);
