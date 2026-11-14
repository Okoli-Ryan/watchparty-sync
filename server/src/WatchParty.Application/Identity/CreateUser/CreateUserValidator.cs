using FluentValidation;

namespace WatchParty.Application.Identity.CreateUser;

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254);
        RuleFor(command => command.Password).ValidPassword();
        RuleFor(command => command.DisplayName).MaximumLength(80);
        RuleFor(command => command.Role).IsInEnum();
    }
}
