using FluentValidation;
using WatchParty.Application.Identity.CreateUser;

namespace WatchParty.Application.Identity.SeedAdmin;

internal sealed class SeedAdminValidator : AbstractValidator<SeedAdminCommand>
{
    public SeedAdminValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254);
        RuleFor(command => command.Password).ValidPassword();
        RuleFor(command => command.DisplayName).MaximumLength(80);
    }
}
