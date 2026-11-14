using FluentValidation;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Rooms.Commands.PostMessage;

internal sealed class PostMessageValidator : AbstractValidator<PostMessageCommand>
{
    public PostMessageValidator()
    {
        RuleFor(command => command.Iv).NotEmpty().MaximumLength(EncryptedText.MaxIvLength);
        RuleFor(command => command.Ct).NotEmpty().MaximumLength(EncryptedText.MaxCiphertextLength);
    }
}
