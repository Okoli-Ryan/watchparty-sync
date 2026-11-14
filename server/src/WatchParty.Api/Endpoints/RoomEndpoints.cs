using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Api.Auth;
using WatchParty.Api.Contracts;
using WatchParty.Api.Errors;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Commands.CreateRoom;
using WatchParty.Application.Rooms.Commands.HostActions;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Endpoints;

/// <summary>Rooms and what their host does with them.</summary>
public static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var rooms = api.MapGroup("/rooms").WithTags("Rooms");

        rooms.MapGet("/", ListAsync).WithName("ListRooms");
        rooms.MapPost("/", CreateAsync).WithName("CreateRoom");
        rooms.MapGet("/{roomId:guid}", GetAsync).WithName("GetRoom");
        rooms.MapPut("/{roomId:guid}/playback", RecordPlaybackAsync).WithName("RecordPlayback");
        rooms.MapPost("/{roomId:guid}/touch", TouchAsync).WithName("TouchRoom");
        rooms.MapPut("/{roomId:guid}/video", RepointVideoAsync).WithName("RepointVideo");
        rooms.MapPost("/{roomId:guid}/ownership-transfers", TransferAsync).WithName("TransferOwnership");
        return api;
    }

    /// <summary>Newest first. Page with <c>before</c> = the last room's <c>createdAt</c>.</summary>
    private static async Task<Ok<IReadOnlyList<RoomDto>>> ListAsync(IRoomQueries queries, CancellationToken cancellationToken, int limit = IRoomQueries.MaxRoomPage, long? before = null) =>
        TypedResults.Ok(await queries.ListRecentRoomsAsync(limit, before, cancellationToken));

    private static async Task<Results<Ok<RoomDto>, NotFound>> GetAsync(Guid roomId, IRoomQueries queries, CancellationToken cancellationToken) =>
        await queries.GetRoomAsync(new RoomId(roomId), cancellationToken) is { } room ? TypedResults.Ok(room) : TypedResults.NotFound();

    private static async Task<Results<Created<CreatedResponse>, ProblemHttpResult>> CreateAsync(
        CreateRoomRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CreateRoomCommand, Result<RoomId>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateRoomCommand(
                user.UserId(),
                request.Name,
                request.PageUrl,
                request.Selector,
                request.FrameOrigin,
                request.CurrentTime,
                request.Visibility,
                request.ChatKey,
                request.ChatSalt,
                request.ChatCheck?.Iv,
                request.ChatCheck?.Ct),
            cancellationToken);
        return result.Match<Results<Created<CreatedResponse>, ProblemHttpResult>>(
            createdRoomId => TypedResults.Created($"/api/v1/rooms/{createdRoomId}", new CreatedResponse(createdRoomId.Value)),
            error => error.ToProblem());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RecordPlaybackAsync(
        Guid roomId,
        PlaybackRequest request,
        ClaimsPrincipal user,
        ICommandHandler<RecordPlaybackCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RecordPlaybackCommand(user.UserId(), new RoomId(roomId), request.IsPlaying, request.CurrentTime, request.Rate), cancellationToken))
            .ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> TouchAsync(
        Guid roomId,
        TouchRoomRequest request,
        ClaimsPrincipal user,
        ICommandHandler<TouchRoomCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new TouchRoomCommand(user.UserId(), new RoomId(roomId), request.Position?.CurrentTime, request.Position?.IsPlaying), cancellationToken))
            .ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> RepointVideoAsync(
        Guid roomId,
        RepointVideoRequest request,
        ClaimsPrincipal user,
        ICommandHandler<RepointVideoCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(
            new RepointVideoCommand(user.UserId(), new RoomId(roomId), request.PageUrl, request.Selector, request.FrameOrigin, request.CurrentTime), cancellationToken))
            .ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> TransferAsync(
        Guid roomId,
        TransferOwnershipRequest request,
        ClaimsPrincipal user,
        ICommandHandler<TransferOwnershipCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new TransferOwnershipCommand(user.UserId(), new RoomId(roomId), new UserId(request.TargetUid)), cancellationToken))
            .ToNoContent();
}
