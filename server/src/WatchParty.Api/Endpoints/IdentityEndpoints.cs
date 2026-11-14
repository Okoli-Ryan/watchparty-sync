using Microsoft.AspNetCore.Http.HttpResults;
using WatchParty.Api.Auth;
using WatchParty.Api.Contracts;
using WatchParty.Api.Errors;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Identity.CreateUser;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.Endpoints;

public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapPost("/auth/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Policies.LoginRateLimit)
            .WithName("Login");

        api.MapGet("/me", GetMeAsync).WithName("GetMe");

        api.MapPost("/users", CreateUserAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("CreateUser");

        return api;
    }

    private static async Task<Results<Ok<LoginResult>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        ICommandHandler<LoginCommand, Result<LoginResult>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.Match<Results<Ok<LoginResult>, ProblemHttpResult>>(loginResult => TypedResults.Ok(loginResult), error => error.ToProblem());
    }

    private static async Task<Results<Ok<UserProfileDto>, NotFound>> GetMeAsync(
        System.Security.Claims.ClaimsPrincipal user,
        IUserQueries users,
        CancellationToken cancellationToken) =>
        await users.GetProfileAsync(user.UserId(), cancellationToken) is { } profile ? TypedResults.Ok(profile) : TypedResults.NotFound();

    private static async Task<Results<Created<UserProfileDto>, ProblemHttpResult>> CreateUserAsync(
        CreateUserRequest request,
        System.Security.Claims.ClaimsPrincipal user,
        ICommandHandler<CreateUserCommand, Result<UserProfileDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateUserCommand(user.UserId(), request.Email, request.Password, request.DisplayName, request.Role), cancellationToken);
        return result.Match<Results<Created<UserProfileDto>, ProblemHttpResult>>(
            profile => TypedResults.Created($"/api/v1/users/{profile.Uid}", profile),
            error => error.ToProblem());
    }
}
