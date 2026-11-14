using FluentValidation;

namespace WatchParty.Application.Identity.Login;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(256);
    }
}
