using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Contracts;

/// <summary>Key material is prepared by the client; the passphrase of a private room never leaves it.</summary>
public sealed record CreateRoomRequest(
    string? Name,
    string PageUrl,
    string Selector,
    string? FrameOrigin,
    double CurrentTime,
    RoomVisibility Visibility,
    string? ChatKey,
    string? ChatSalt,
    EncryptedTextDto? ChatCheck);
