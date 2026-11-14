using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.CreateRoom;

internal sealed class CreateRoomHandler(IUserRepository users, IRoomRepository rooms, IUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<CreateRoomCommand, Result<RoomId>>
{
    public async Task<Result<RoomId>> HandleAsync(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        var owner = await users.GetByIdAsync(command.ActorId, cancellationToken);
        if (owner is null)
        {
            return UserErrors.NotFound;
        }

        var name = RoomName.Create(command.Name);
        if (name.IsFailure)
        {
            return name.Error!;
        }

        var video = VideoSource.Create(command.PageUrl, command.Selector, command.FrameOrigin);
        if (video.IsFailure)
        {
            return video.Error!;
        }

        var chat = BuildChatProtection(command);
        if (chat.IsFailure)
        {
            return chat.Error!;
        }

        var room = Room.Create(name.Value, video.Value, chat.Value, owner.Id, owner.DisplayName, command.CurrentTime, clock);
        if (room.IsFailure)
        {
            return room.Error!;
        }

        rooms.Add(room.Value);
        var committed = await unitOfWork.CommitAsync(cancellationToken);
        return committed.IsFailure ? committed.Error! : room.Value.Id;
    }

    private static Result<ChatProtection> BuildChatProtection(CreateRoomCommand command)
    {
        if (command.Visibility == RoomVisibility.Public)
        {
            // A public room must not also smuggle in private-room material, or the
            // stored row would contradict its own visibility.
            return string.IsNullOrEmpty(command.ChatSalt) && command.ChatCheckIv is null && command.ChatCheckCt is null
                ? ChatProtection.Public(command.ChatKey)
                : RoomErrors.InvalidChatKeyMaterial;
        }

        if (!string.IsNullOrEmpty(command.ChatKey))
        {
            // The whole point of a private room is that the key is never stored.
            return RoomErrors.InvalidChatKeyMaterial;
        }

        var verifier = EncryptedText.Create(command.ChatCheckIv, command.ChatCheckCt);
        return verifier.IsFailure ? RoomErrors.InvalidChatKeyMaterial : ChatProtection.Private(command.ChatSalt, verifier.Value);
    }
}
