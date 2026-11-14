namespace WatchParty.Domain.SharedKernel;

/// <summary>The name other people see: trimmed, non-empty, bounded.</summary>
public sealed record DisplayName
{
    public const int MaxLength = 80;

    public static readonly Error Empty = Error.Validation("DisplayName.Empty", "A display name is required.");

    public static readonly Error TooLong =
        Error.Validation("DisplayName.TooLong", $"Display names are at most {MaxLength} characters.");

    private DisplayName(string value) => Value = value;

    public string Value { get; }

    public static Result<DisplayName> Create(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return Empty;
        }

        return value.Length > MaxLength ? TooLong : new DisplayName(value);
    }

    public override string ToString() => Value;
}
