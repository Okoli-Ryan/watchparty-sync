using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.Rooms.Commands.PostMessage;

internal sealed class PostMessageHandler(
    IUserRepository users,
    IRoomRepository rooms,
    IChatMessageRepository messages,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<PostMessageCommand, Result<ChatMessageId>>
{
    public async Task<Result<ChatMessageId>> HandleAsync(PostMessageCommand command, CancellationToken cancellationToken)
    {
        var body = EncryptedText.Create(command.Iv, command.Ct);
        if (body.IsFailure)
        {
            return body.Error!;
        }

        var room = await rooms.GetByIdAsync(command.RoomId, cancellationToken);
        if (room is null)
        {
            return RoomErrors.NotFound;
        }

        // The sender is whoever the token says, never a name the client supplies.
        var sender = await users.GetByIdAsync(command.ActorId, cancellationToken);
        if (sender is null)
        {
            return UserErrors.NotFound;
        }

        var message = ChatMessage.Post(room, sender.Id, sender.DisplayName, body.Value, clock);
        messages.Add(message);
        var committed = await unitOfWork.CommitAsync(cancellationToken);
        return committed.IsFailure ? committed.Error! : message.Id;
    }
}
