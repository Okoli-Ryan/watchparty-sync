namespace WatchParty.Domain.SharedKernel;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
}

/// <summary>An expected failure, returned as a value rather than thrown.</summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>Per-field messages, for validation failures that name more than one problem.</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
}
