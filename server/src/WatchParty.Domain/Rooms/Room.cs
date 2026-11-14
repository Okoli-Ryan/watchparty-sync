using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// A watch party: one video, one host driving playback, any number of viewers.
///
/// Ownership is two claims, not one (DECISIONS.md #8). <see cref="OwnerId"/> is who
/// drives playback right now and moves automatically when the host goes quiet.
/// <see cref="PrimaryOwnerId"/> is the persistent claim: automatic handoff never moves
/// it, so the original host reclaims the room on return. A deliberate transfer moves both.
/// </summary>
public sealed class Room : AggregateRoot<RoomId>
{
#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private Room()
    {
    }
#pragma warning restore CS8618

    private Room(
        RoomId id,
        RoomName name,
        VideoSource video,
        ChatProtection chat,
        UserId ownerId,
        DisplayName ownerName,
        UserId primaryOwnerId,
        DisplayName primaryOwnerName,
        PlaybackState playback,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Video = video;
        Chat = chat;
        OwnerId = ownerId;
        OwnerName = ownerName;
        PrimaryOwnerId = primaryOwnerId;
        PrimaryOwnerName = primaryOwnerName;
        Playback = playback;
        CreatedAt = createdAt;
        LastActiveAt = createdAt;
        IsActive = true;
    }

    public RoomName Name { get; private set; }

    public VideoSource Video { get; private set; }

    public ChatProtection Chat { get; private set; }

    public UserId OwnerId { get; private set; }

    public DisplayName OwnerName { get; private set; }

    public UserId PrimaryOwnerId { get; private set; }

    public DisplayName PrimaryOwnerName { get; private set; }

    public bool IsActive { get; private set; }

    public PlaybackState Playback { get; private set; }

    public HostPosition? HostPosition { get; private set; }

    /// <summary>Host-stamped liveness heartbeat. A room that stops being touched has ended.</summary>
    public DateTimeOffset LastActiveAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsOwnedBy(UserId userId) => OwnerId == userId;

    public static Result<Room> Create(
        RoomName name,
        VideoSource video,
        ChatProtection chat,
        UserId ownerId,
        DisplayName ownerName,
        double currentTime,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var playback = PlaybackState.PausedAt(currentTime, updatedAt: null, ownerId);
        if (playback.IsFailure)
        {
            return playback.Error!;
        }

        var room = new Room(RoomId.New(), name, video, chat, ownerId, ownerName, ownerId, ownerName, playback.Value, clock.GetUtcNow());
        room.Raise(new RoomCreated(room.Id, ownerId));
        return room;
    }

    /// <summary>The host played, paused, seeked or changed rate.</summary>
    public Result RecordPlayback(UserId actor, bool isPlaying, double currentTime, double rate, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var allowed = EnsureHost(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var playback = PlaybackState.Create(isPlaying, currentTime, rate, clock.GetUtcNow(), actor);
        if (playback.IsFailure)
        {
            return playback.Error!;
        }

        Playback = playback.Value;
        Raise(new PlaybackRecorded(Id));
        return Result.Success();
    }

    /// <summary>
    /// The host's liveness heartbeat. Optionally refreshes <see cref="HostPosition"/>;
    /// it never touches <see cref="Playback"/>, so viewers are not realigned by it.
    /// </summary>
    public Result Touch(UserId actor, (double CurrentTime, bool IsPlaying)? position, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var allowed = EnsureHost(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var now = clock.GetUtcNow();
        if (position is { } requestedPosition)
        {
            var hostPosition = HostPosition.Create(requestedPosition.CurrentTime, requestedPosition.IsPlaying, now);
            if (hostPosition.IsFailure)
            {
                return hostPosition.Error!;
            }

            HostPosition = hostPosition.Value;
        }

        LastActiveAt = now;
        Raise(new RoomTouched(Id));
        return Result.Success();
    }

    /// <summary>
    /// Point the room at a different video. Playback restarts paused at the new video's
    /// position so viewers don't inherit a timestamp from the old one.
    /// </summary>
    public Result RepointVideo(UserId actor, VideoSource video, double currentTime, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(clock);
        var allowed = EnsureHost(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var playback = PlaybackState.PausedAt(currentTime, clock.GetUtcNow(), actor);
        if (playback.IsFailure)
        {
            return playback.Error!;
        }

        Video = video;
        Playback = playback.Value;
        HostPosition = null;
        Raise(new RoomVideoRepointed(Id));
        return Result.Success();
    }

    /// <summary>
    /// Deliberately hand the room to someone else. Moves the persistent claim too, so
    /// the previous host won't reclaim it on their next visit.
    /// </summary>
    public Result TransferTo(UserId actor, UserId target, DisplayName targetName)
    {
        ArgumentNullException.ThrowIfNull(targetName);
        var allowed = EnsureHost(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (target == actor)
        {
            return RoomErrors.CannotTransferToSelf;
        }

        PrimaryOwnerId = target;
        PrimaryOwnerName = targetName;
        ChangeOwner(target, targetName, deliberate: true);
        return Result.Success();
    }

    /// <summary>
    /// The primary owner has come back. They take the room back from whoever inherited
    /// it. Returns true when ownership actually moved.
    /// </summary>
    public Result<bool> Reclaim(UserId actor, DisplayName actorName)
    {
        ArgumentNullException.ThrowIfNull(actorName);
        if (!IsActive)
        {
            return RoomErrors.Inactive;
        }

        if (PrimaryOwnerId != actor)
        {
            return RoomErrors.NotPrimaryOwner;
        }

        if (OwnerId == actor)
        {
            return false;
        }

        ChangeOwner(actor, actorName, deliberate: false);
        return true;
    }

    /// <summary>
    /// Automatic handoff to the heir chosen by <see cref="Services.OwnershipSuccession"/>.
    /// Leaves the persistent claim where it is.
    /// </summary>
    public void SucceedTo(UserId heir, DisplayName heirName)
    {
        ArgumentNullException.ThrowIfNull(heirName);
        if (IsActive && OwnerId != heir)
        {
            ChangeOwner(heir, heirName, deliberate: false);
        }
    }

    /// <summary>The last member left. Idempotent.</summary>
    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Raise(new RoomDeactivated(Id));
    }

    private void ChangeOwner(UserId newOwner, DisplayName newOwnerName, bool deliberate)
    {
        var previous = OwnerId;
        OwnerId = newOwner;
        OwnerName = newOwnerName;
        Raise(new RoomOwnershipChanged(Id, previous, newOwner, deliberate));
    }

    private Result EnsureHost(UserId actor)
    {
        if (!IsActive)
        {
            return RoomErrors.Inactive;
        }

        return OwnerId == actor ? Result.Success() : RoomErrors.NotOwner;
    }
}
