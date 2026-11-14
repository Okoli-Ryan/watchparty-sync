using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Errors;

/// <summary>The one place a domain or application <see cref="Error"/> becomes HTTP (RFC 9457).</summary>
public static class ErrorResults
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code };
        if (error.Fields is { Count: > 0 } fields)
        {
            extensions["errors"] = fields;
        }

        return TypedResults.Problem(
            title: Title(error.Type),
            detail: error.Description,
            statusCode: status,
            extensions: extensions);
    }

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "The request is invalid.",
        ErrorType.NotFound => "Not found.",
        ErrorType.Conflict => "Conflict.",
        ErrorType.Forbidden => "Forbidden.",
        ErrorType.Unauthorized => "Unauthorized.",
        _ => "Error.",
    };
}
