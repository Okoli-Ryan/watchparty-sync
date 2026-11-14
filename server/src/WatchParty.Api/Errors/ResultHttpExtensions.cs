using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Errors;

public static class ResultHttpExtensions
{
    public static Results<NoContent, ProblemHttpResult> ToNoContent(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
