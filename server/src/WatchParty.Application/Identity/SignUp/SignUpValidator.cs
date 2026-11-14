using FluentValidation;
using WatchParty.Domain.Identity;

namespace WatchParty.Application.Identity.SignUp;

internal sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Password).ValidPassword();
    }
}
