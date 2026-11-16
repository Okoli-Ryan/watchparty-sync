using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Api.Auth;
using WatchParty.Api.Contracts;
using WatchParty.Api.Errors;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Rooms.Commands.Heartbeat;
using WatchParty.Application.Rooms.Commands.JoinRoom;
using WatchParty.Application.Rooms.Commands.LeaveRoom;
using WatchParty.Application.Rooms.Queries;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Endpoints;

/// <summary>
/// Membership of a room. A caller only ever joins, heartbeats or leaves as themselves
/// (<c>/members/me</c>); the server prunes and hands off everyone else.
/// </summary>
public static class PresenceEndpoints
{
    public static RouteGroupBuilder MapPresenceEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var members = api.MapGroup("/rooms/{roomId:guid}/members").WithTags("Presence");

        members.MapGet("/", ListAsync).WithName("ListMembers");
        members.MapPut("/me", JoinAsync).WithName("JoinRoom");
        members.MapPost("/me/heartbeat", HeartbeatAsync).WithName("Heartbeat");
        members.MapDelete("/me", LeaveAsync).WithName("LeaveRoom");
        return api;
    }

    private static async Task<Ok<MembersSnapshotDto>> ListAsync(Guid roomId, IRoomQueries queries, TimeProvider clock, CancellationToken cancellationToken)
    {
        var members = await queries.ListMembersAsync(new RoomId(roomId), cancellationToken);
        return TypedResults.Ok(new MembersSnapshotDto(roomId, members, clock.GetUtcNow().ToUnixTimeMilliseconds()));
    }

    private static async Task<Results<Ok<JoinResponse>, ProblemHttpResult>> JoinAsync(
        Guid roomId,
        ClaimsPrincipal user,
        ICommandHandler<JoinRoomCommand, Result<JoinResult>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new JoinRoomCommand(user.UserId(), new RoomId(roomId)), cancellationToken);
        return result.Match<Results<Ok<JoinResponse>, ProblemHttpResult>>(
            joined => TypedResults.Ok(new JoinResponse(joined.Role == MemberRole.Owner ? "owner" : "viewer")),
            error => error.ToProblem());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HeartbeatAsync(
        Guid roomId,
        ClaimsPrincipal user,
        ICommandHandler<HeartbeatCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new HeartbeatCommand(user.UserId(), new RoomId(roomId)), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> LeaveAsync(
        Guid roomId,
        ClaimsPrincipal user,
        ICommandHandler<LeaveRoomCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new LeaveRoomCommand(user.UserId(), new RoomId(roomId)), cancellationToken)).ToNoContent();
}
