using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Api.Auth;
using WatchParty.Api.Contracts;
using WatchParty.Api.Errors;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Commands.PostMessage;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Endpoints;

/// <summary>Encrypted room chat. The server relays ciphertext and never holds a key.</summary>
public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var messages = api.MapGroup("/rooms/{roomId:guid}/messages").WithTags("Chat");

        messages.MapGet("/", ListAsync).WithName("ListMessages");
        messages.MapPost("/", PostAsync).WithName("PostMessage");
        return api;
    }

    /// <summary>The newest messages (at most 100), oldest first.</summary>
    private static async Task<Ok<IReadOnlyList<ChatMessageDto>>> ListAsync(
        Guid roomId,
        IRoomQueries queries,
        CancellationToken cancellationToken,
        int limit = IRoomQueries.MaxChatPage) =>
        TypedResults.Ok(await queries.ListRecentMessagesAsync(new RoomId(roomId), limit, cancellationToken));

    private static async Task<Results<Created<CreatedResponse>, ProblemHttpResult>> PostAsync(
        Guid roomId,
        PostMessageRequest request,
        ClaimsPrincipal user,
        ICommandHandler<PostMessageCommand, Result<ChatMessageId>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new PostMessageCommand(user.UserId(), new RoomId(roomId), request.Iv, request.Ct), cancellationToken);
        return result.Match<Results<Created<CreatedResponse>, ProblemHttpResult>>(
            messageId => TypedResults.Created($"/api/v1/rooms/{roomId}/messages/{messageId}", new CreatedResponse(messageId.Value)),
            error => error.ToProblem());
    }
}
