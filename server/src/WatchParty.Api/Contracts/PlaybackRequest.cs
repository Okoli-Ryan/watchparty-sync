namespace WatchParty.Api.Contracts;

public sealed record PlaybackRequest(bool IsPlaying, double CurrentTime, double Rate);
