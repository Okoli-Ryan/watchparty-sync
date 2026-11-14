namespace WatchParty.Api.Contracts;

public sealed record RepointVideoRequest(string PageUrl, string Selector, string? FrameOrigin, double CurrentTime);
