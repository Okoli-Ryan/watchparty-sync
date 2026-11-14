using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

public sealed record RoomName
{
    public const int MaxLength = 120;

    public const string Untitled = "Untitled room";

    private RoomName(string value) => Value = value;

    public string Value { get; }

    /// <summary>A blank name becomes <see cref="Untitled"/>, as it always has.</summary>
    public static Result<RoomName> Create(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Untitled : raw.Trim();
        return value.Length > MaxLength ? RoomErrors.NameTooLong : new RoomName(value);
    }

    public override string ToString() => Value;
}
