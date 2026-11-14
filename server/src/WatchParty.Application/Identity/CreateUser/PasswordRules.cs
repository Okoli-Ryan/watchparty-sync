using FluentValidation;

namespace WatchParty.Application.Identity.CreateUser;

internal static class PasswordRules
{
    public const int MinLength = 8;

    public const int MaxLength = 256;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(MinLength).MaximumLength(MaxLength);
}
