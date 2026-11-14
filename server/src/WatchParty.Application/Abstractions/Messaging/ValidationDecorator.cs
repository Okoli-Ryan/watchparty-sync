using FluentValidation;
using FluentValidation.Results;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Abstractions.Messaging;

/// <summary>
/// Runs every registered validator for a command before its handler. Input shape is
/// checked here; business invariants are the domain's job.
/// </summary>
internal sealed class ValidationDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
    where TResult : Result
{
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count == 0)
        {
            return await inner.HandleAsync(command, cancellationToken);
        }

        var messagesByField = GroupByField(failures);
        var error = Error.Validation("Validation.Failed", "One or more fields are invalid.") with { Fields = messagesByField };
        return FailureFactory<TResult>.Create(error);
    }

    private static Dictionary<string, string[]> GroupByField(List<ValidationFailure> failures)
    {
        var messagesByField = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var failuresByField = failures.GroupBy(failure => failure.PropertyName, StringComparer.Ordinal);

        foreach (var fieldFailures in failuresByField)
        {
            var distinctMessages = fieldFailures
                .Select(failure => failure.ErrorMessage)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            messagesByField[fieldFailures.Key] = distinctMessages;
        }

        return messagesByField;
    }
}
