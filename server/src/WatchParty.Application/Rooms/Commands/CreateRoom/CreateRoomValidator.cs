using FluentValidation;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Rooms.Commands.CreateRoom;

internal sealed class CreateRoomValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomValidator()
    {
        RuleFor(command => command.Name).MaximumLength(RoomName.MaxLength);
        RuleFor(command => command.PageUrl).NotEmpty().MaximumLength(VideoSource.MaxUrlLength);
        RuleFor(command => command.Selector).NotEmpty().MaximumLength(VideoSource.MaxSelectorLength);
        RuleFor(command => command.FrameOrigin).MaximumLength(VideoSource.MaxOriginLength);
        RuleFor(command => command.Visibility).IsInEnum();
    }
}
