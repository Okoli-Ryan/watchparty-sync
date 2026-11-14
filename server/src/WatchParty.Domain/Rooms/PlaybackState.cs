using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// What the host last DID: play, pause, seek or a rate change. Viewers realign only
/// when this changes (sync is event-driven, DECISIONS.md #10).
/// </summary>
public sealed record PlaybackState
{
    public const double MinRate = 0.0625;

    public const double MaxRate = 16;

    /// <summary>Upper bound on a position (about 115 days): rejects garbage, admits any real video.</summary>
    public const double MaxSeconds = 10_000_000;

#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private PlaybackState()
    {
    }
#pragma warning restore CS8618

    private PlaybackState(bool isPlaying, double currentTime, double rate, DateTimeOffset? updatedAt, UserId updatedBy)
    {
        IsPlaying = isPlaying;
        CurrentTime = currentTime;
        Rate = rate;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public bool IsPlaying { get; private init; }

    public double CurrentTime { get; private init; }

    public double Rate { get; private init; }

    /// <summary>Server time of the write; null until the host first acts.</summary>
    public DateTimeOffset? UpdatedAt { get; private init; }

    public UserId UpdatedBy { get; private init; }

    public static Result<PlaybackState> Create(bool isPlaying, double currentTime, double rate, DateTimeOffset? updatedAt, UserId updatedBy)
    {
        if (!IsValidTime(currentTime))
        {
            return RoomErrors.InvalidPlaybackTime;
        }

        if (!double.IsFinite(rate) || rate < MinRate || rate > MaxRate)
        {
            return RoomErrors.InvalidPlaybackRate;
        }

        return new PlaybackState(isPlaying, currentTime, rate, updatedAt, updatedBy);
    }

    /// <summary>A paused state at <paramref name="currentTime"/>: how every new video starts.</summary>
    public static Result<PlaybackState> PausedAt(double currentTime, DateTimeOffset? updatedAt, UserId updatedBy) =>
        Create(isPlaying: false, currentTime, rate: 1, updatedAt, updatedBy);

    internal static bool IsValidTime(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0 && seconds <= MaxSeconds;
}
