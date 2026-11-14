using System.Reflection;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Messaging;

/// <summary>
/// Builds a failed <typeparamref name="TResult"/> (either <see cref="Result"/> or a
/// <see cref="Result{T}"/>) from an error, so decorators can short-circuit generically.
/// The conversion is resolved once per result type.
/// </summary>
internal static class FailureFactory<TResult>
    where TResult : Result
{
    public static readonly Func<Error, TResult> Create = Build();

    private static Func<Error, TResult> Build()
    {
        if (typeof(TResult) == typeof(Result))
        {
            return error => (TResult)Result.Failure(error);
        }

        var conversion = typeof(TResult).GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [typeof(Error)])
            ?? throw new InvalidOperationException($"{typeof(TResult)} has no conversion from {nameof(Error)}.");
        return error => (TResult)conversion.Invoke(null, [error])!;
    }
}
