using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

public static class RoomErrors
{
    public static readonly Error NotFound = Error.NotFound("Room.NotFound", "No such room.");

    public static readonly Error Inactive = Error.Conflict("Room.Inactive", "This room is no longer active.");

    public static readonly Error NotOwner = Error.Forbidden("Room.NotOwner", "Only the room's current host can do that.");

    public static readonly Error NotPrimaryOwner =
        Error.Forbidden("Room.NotPrimaryOwner", "Only the room's original host can reclaim it.");

    public static readonly Error NameTooLong =
        Error.Validation("Room.NameTooLong", $"Room names are at most {RoomName.MaxLength} characters.");

    public static readonly Error InvalidPageUrl =
        Error.Validation("Room.InvalidPageUrl", "The page URL must be an absolute http(s) URL.");

    public static readonly Error InvalidSelector =
        Error.Validation("Room.InvalidSelector", "A CSS selector for the video is required.");

    public static readonly Error InvalidFrameOrigin =
        Error.Validation("Room.InvalidFrameOrigin", "The frame origin must be empty or an http(s) origin.");

    public static readonly Error InvalidPlaybackTime =
        Error.Validation("Room.InvalidPlaybackTime", "Playback time must be a finite, non-negative number of seconds.");

    public static readonly Error InvalidPlaybackRate =
        Error.Validation("Room.InvalidPlaybackRate", $"Playback rate must be between {PlaybackState.MinRate} and {PlaybackState.MaxRate}.");

    public static readonly Error InvalidChatKeyMaterial =
        Error.Validation("Room.InvalidChatKeyMaterial", "Public rooms carry a key; private rooms carry a salt and a verifier, never a key.");

    public static readonly Error CannotTransferToSelf =
        Error.Validation("Room.CannotTransferToSelf", "You already host this room.");

    public static readonly Error TransferTargetAbsent =
        Error.Conflict("Room.TransferTargetAbsent", "That person is no longer watching, so they cannot take over.");

    public static readonly Error NotAMember =
        Error.NotFound("Room.NotAMember", "You are not in this room.");
}
