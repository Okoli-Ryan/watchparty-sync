using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WatchParty.Api.Auth;
using WatchParty.Api.Errors;
using WatchParty.Api.Realtime;
using WatchParty.Application.Abstractions.Realtime;
using WatchParty.Infrastructure;
using WatchParty.Infrastructure.Security;

namespace WatchParty.Api.Hosting;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicy = "clients";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = AddTraceId);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.ConfigureHttpJsonOptions(ConfigureHttpJson);
        services.AddOpenApi();
        services.AddApiVersioning(ConfigureVersioning);

        AddRealtime(services);
        AddAuthentication(services);
        AddAuthorization(services);
        AddCors(services, configuration);
        services.AddRateLimiter(ConfigureRateLimits);

        services.AddHealthChecks().AddDatabaseHealthCheck();
        return services;
    }

    private static void AddTraceId(ProblemDetailsContext problemContext) =>
        problemContext.ProblemDetails.Extensions["traceId"] = problemContext.HttpContext.TraceIdentifier;

    private static void ConfigureHttpJson(JsonOptions jsonOptions) =>
        AddEnumConverter(jsonOptions.SerializerOptions);

    /// <summary>Enums travel as "public"/"private", "admin"/"user": the strings the extension already uses.</summary>
    private static void AddEnumConverter(JsonSerializerOptions serializerOptions)
    {
        var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
        serializerOptions.Converters.Add(enumConverter);
    }

    private static void ConfigureVersioning(ApiVersioningOptions versioningOptions)
    {
        versioningOptions.DefaultApiVersion = new ApiVersion(1, 0);
        versioningOptions.ApiVersionReader = new UrlSegmentApiVersionReader();
        versioningOptions.ReportApiVersions = true;
    }

    private static void AddRealtime(IServiceCollection services)
    {
        services
            .AddSignalR()
            .AddJsonProtocol(protocolOptions => AddEnumConverter(protocolOptions.PayloadSerializerOptions));

        services.AddSingleton<RealtimeQueue>();
        services.AddSingleton<IRealtimeNotifier>(serviceProvider => serviceProvider.GetRequiredService<RealtimeQueue>());
        services.AddHostedService<RealtimeBroadcaster>();
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
            {
                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = jwtOptions.Value.CreateValidationParameters();
                bearerOptions.Events = new JwtBearerEvents { OnMessageReceived = ReadHubTokenFromQuery };
            });
    }

    /// <summary>
    /// Browsers cannot set headers on a WebSocket, so SignalR sends the token in the query
    /// string. It is accepted there for the hub only, never for the REST API.
    /// </summary>
    private static Task ReadHubTokenFromQuery(MessageReceivedContext messageContext)
    {
        var queryToken = messageContext.Request.Query["access_token"].ToString();
        var isHubRequest = messageContext.HttpContext.Request.Path.StartsWithSegments(
            RealtimeHub.Path,
            StringComparison.Ordinal);

        if (isHubRequest && !string.IsNullOrEmpty(queryToken))
        {
            messageContext.Token = queryToken;
        }

        return Task.CompletedTask;
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        var signedInByDefault = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(signedInByDefault);
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        // Bearer tokens, not cookies: no credentials are allowed, so no origin can ride a session.
        services.AddCors(corsOptions => corsOptions.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "X-SignalR-User-Agent")));
    }

    private static void ConfigureRateLimits(RateLimiterOptions rateLimiterOptions)
    {
        rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        rateLimiterOptions.AddPolicy(Policies.LoginRateLimit, LoginPartition);
        rateLimiterOptions.AddPolicy(Policies.PerUserRateLimit, PerUserPartition);
    }

    /// <summary>Password guessing: a handful of attempts per minute per client address.</summary>
    private static RateLimitPartition<string> LoginPartition(HttpContext httpContext)
    {
        var clientAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var limits = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        };

        return RateLimitPartition.GetFixedWindowLimiter(clientAddress, _ => limits);
    }

    /// <summary>
    /// Everything else, per signed-in user. A watching client makes about 20 heartbeats and
    /// 7 room touches a minute, plus a burst of playback writes while the host scrubs.
    /// </summary>
    private static RateLimitPartition<string> PerUserPartition(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        var clientAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var partitionKey = userId ?? clientAddress ?? "unknown";

        var limits = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 120,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(2),
            QueueLimit = 0,
        };

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => limits);
    }
}
