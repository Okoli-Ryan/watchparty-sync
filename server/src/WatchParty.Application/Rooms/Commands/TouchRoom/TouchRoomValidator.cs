using FluentValidation;

namespace WatchParty.Application.Rooms.Commands.TouchRoom;

internal sealed class TouchRoomValidator : AbstractValidator<TouchRoomCommand>
{
    public TouchRoomValidator() =>
        RuleFor(command => command.IsPlaying)
            .NotNull()
            .When(command => command.CurrentTime is not null)
            .WithMessage("A position needs both currentTime and isPlaying.");
}
