using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Api.Auth;
using WatchParty.Api.Contracts;
using WatchParty.Api.Errors;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Commands;
using WatchParty.Application.History.Queries;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Endpoints;

/// <summary>The caller's own history. There is no route to anyone else's.</summary>
public static class HistoryEndpoints
{
    public static RouteGroupBuilder MapHistoryEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var history = api.MapGroup("/me/history").WithTags("History");

        history.MapGet("/", ListAsync).WithName("ListHistory");
        history.MapGet("/{roomId:guid}", GetAsync).WithName("GetHistoryEntry");
        history.MapPut("/{roomId:guid}/favourite", SetFavouriteAsync).WithName("SetFavourite");
        history.MapPut("/{roomId:guid}/read-position", MarkReadAsync).WithName("MarkRead");
        history.MapDelete("/{roomId:guid}", RemoveAsync).WithName("RemoveFromHistory");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<HistoryEntryDto>>> ListAsync(
        ClaimsPrincipal user,
        IHistoryQueries queries,
        CancellationToken cancellationToken,
        int limit = IHistoryQueries.MaxPage) =>
        TypedResults.Ok(await queries.ListAsync(user.UserId(), limit, cancellationToken));

    private static async Task<Results<Ok<HistoryEntryDto>, NotFound>> GetAsync(
        Guid roomId,
        ClaimsPrincipal user,
        IHistoryQueries queries,
        CancellationToken cancellationToken) =>
        await queries.GetAsync(user.UserId(), new RoomId(roomId), cancellationToken) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound();

    private static async Task<Results<NoContent, ProblemHttpResult>> SetFavouriteAsync(
        Guid roomId,
        FavouriteRequest request,
        ClaimsPrincipal user,
        ICommandHandler<SetFavouriteCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SetFavouriteCommand(user.UserId(), new RoomId(roomId), request.Favourite), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarkReadAsync(
        Guid roomId,
        ReadPositionRequest request,
        ClaimsPrincipal user,
        ICommandHandler<MarkReadCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new MarkReadCommand(user.UserId(), new RoomId(roomId), request.At), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveAsync(
        Guid roomId,
        ClaimsPrincipal user,
        ICommandHandler<RemoveFromHistoryCommand, Result> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RemoveFromHistoryCommand(user.UserId(), new RoomId(roomId)), cancellationToken)).ToNoContent();
}
