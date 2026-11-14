namespace WatchParty.Application.Rooms.Queries;

public sealed record EncryptedTextDto(string Iv, string Ct);
