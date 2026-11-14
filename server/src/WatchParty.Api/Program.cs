using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using WatchParty.Api.Auth;
using WatchParty.Api.Endpoints;
using WatchParty.Api.Hosting;
using WatchParty.Api.Realtime;
using WatchParty.Application;
using WatchParty.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(providerOptions =>
{
    providerOptions.ValidateScopes = true;
    providerOptions.ValidateOnBuild = true;
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

// Order per the guidelines (§6): exception handler first, auth just before endpoints.
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(ApiServiceCollectionExtensions.CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

var livenessOnly = new HealthCheckOptions { Predicate = _ => false };
var readinessChecks = new HealthCheckOptions { Predicate = healthCheck => healthCheck.Tags.Contains("ready") };
app.MapHealthChecks("/health/live", livenessOnly).AllowAnonymous();
app.MapHealthChecks("/health/ready", readinessChecks).AllowAnonymous();

var apiVersions = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersions)
    .RequireRateLimiting(Policies.PerUserRateLimit)
    .MapIdentityEndpoints()
    .MapRoomEndpoints()
    .MapPresenceEndpoints()
    .MapChatEndpoints()
    .MapHistoryEndpoints();

app.MapHub<RealtimeHub>(RealtimeHub.Path);

await app.Services.MigrateDatabaseIfConfiguredAsync(CancellationToken.None);
await app.RunAsync();
return 0;

/// <summary>Entry point, visible to <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
