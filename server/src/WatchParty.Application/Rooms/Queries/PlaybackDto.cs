namespace WatchParty.Application.Rooms.Queries;

public sealed record PlaybackDto(bool IsPlaying, double CurrentTime, double Rate, long? UpdatedAt, Guid UpdatedBy);
