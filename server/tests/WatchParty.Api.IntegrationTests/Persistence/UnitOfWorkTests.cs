using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Api.IntegrationTests.Persistence;

/// <summary>Optimistic concurrency through EF Core against a real Postgres (§2.5, §10).</summary>
public sealed class UnitOfWorkTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    [Fact]
    public async Task Commit_FromAStaleCopy_IsAConflictAndLeavesTheFirstWriteInPlace()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = new RoomId(await host.CreatePublicRoomAsync());
        var hostId = new UserId(host.UserId);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRooms = firstScope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var secondRooms = secondScope.ServiceProvider.GetRequiredService<IRoomRepository>();

        // Both load version 0 of the same room.
        var firstCopy = (await firstRooms.GetByIdAsync(roomId, CancellationToken.None))!;
        var secondCopy = (await secondRooms.GetByIdAsync(roomId, CancellationToken.None))!;

        firstCopy.RecordPlayback(hostId, isPlaying: true, currentTime: 10, rate: 1, factory.Clock);
        var firstCommit = await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

        secondCopy.RecordPlayback(hostId, isPlaying: false, currentTime: 99, rate: 1, factory.Clock);
        var secondCommit = await secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

        firstCommit.IsSuccess.ShouldBeTrue();
        secondCommit.Error.ShouldBe(ConcurrencyErrors.Conflict);

        var stored = await host.GetRoomAsync(roomId.Value);
        stored.Playback.CurrentTime.ShouldBe(10);
    }

    [Fact]
    public async Task Commit_AfterAConflict_RetryReloadsFreshStateAndSucceeds()
    {
        using var host = await factory.SignInAsNewUserAsync("Host");
        var roomId = new RoomId(await host.CreatePublicRoomAsync());
        var hostId = new UserId(host.UserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var staleCopy = (await rooms.GetByIdAsync(roomId, CancellationToken.None))!;

        // Someone else writes in between.
        var playback = await host.Http.PutAsJsonAsync(
            $"/api/v1/rooms/{roomId.Value}/playback",
            new { isPlaying = true, currentTime = 5, rate = 1 });
        playback.EnsureSuccessStatusCode();

        staleCopy.RecordPlayback(hostId, isPlaying: true, currentTime: 20, rate: 1, factory.Clock);
        (await unitOfWork.CommitAsync(CancellationToken.None)).Error.ShouldBe(ConcurrencyErrors.Conflict);

        var freshCopy = (await rooms.GetByIdAsync(roomId, CancellationToken.None))!;
        freshCopy.Playback.CurrentTime.ShouldBe(5);
        freshCopy.RecordPlayback(hostId, isPlaying: true, currentTime: 20, rate: 1, factory.Clock);
        (await unitOfWork.CommitAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }
}
