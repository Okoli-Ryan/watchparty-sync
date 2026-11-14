using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// The host's live playhead, refreshed on the room heartbeat. Deliberately separate
/// from <see cref="PlaybackState"/>: it must never look like a host action, or viewers
/// would re-apply it on a timer. It exists so "Sync with host" has a recent anchor.
/// </summary>
public sealed record HostPosition
{
#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private HostPosition()
    {
    }
#pragma warning restore CS8618

    private HostPosition(double currentTime, bool isPlaying, DateTimeOffset updatedAt)
    {
        CurrentTime = currentTime;
        IsPlaying = isPlaying;
        UpdatedAt = updatedAt;
    }

    public double CurrentTime { get; private init; }

    public bool IsPlaying { get; private init; }

    public DateTimeOffset UpdatedAt { get; private init; }

    public static Result<HostPosition> Create(double currentTime, bool isPlaying, DateTimeOffset updatedAt) =>
        PlaybackState.IsValidTime(currentTime)
            ? new HostPosition(currentTime, isPlaying, updatedAt)
            : RoomErrors.InvalidPlaybackTime;
}
