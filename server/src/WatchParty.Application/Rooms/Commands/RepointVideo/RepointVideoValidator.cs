using FluentValidation;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Rooms.Commands.RepointVideo;

internal sealed class RepointVideoValidator : AbstractValidator<RepointVideoCommand>
{
    public RepointVideoValidator()
    {
        RuleFor(command => command.PageUrl).NotEmpty().MaximumLength(VideoSource.MaxUrlLength);
        RuleFor(command => command.Selector).NotEmpty().MaximumLength(VideoSource.MaxSelectorLength);
        RuleFor(command => command.FrameOrigin).MaximumLength(VideoSource.MaxOriginLength);
    }
}
