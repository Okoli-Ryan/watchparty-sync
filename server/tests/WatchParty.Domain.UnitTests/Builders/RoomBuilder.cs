using Microsoft.Extensions.Time.Testing;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.UnitTests.Builders;

/// <summary>Builds valid rooms so each test states only what it cares about.</summary>
internal sealed class RoomBuilder
{
    private UserId _ownerId = UserId.New();
    private string _ownerName = "Host";
    private string? _name = "Movie night";
    private double _currentTime;

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 11, 14, 20, 0, 0, TimeSpan.Zero));

    public RoomBuilder OwnedBy(UserId ownerId, string ownerName = "Host")
    {
        _ownerId = ownerId;
        _ownerName = ownerName;
        return this;
    }

    public RoomBuilder Named(string? name)
    {
        _name = name;
        return this;
    }

    public RoomBuilder StartingAt(double currentTime)
    {
        _currentTime = currentTime;
        return this;
    }

    public Room Build(TimeProvider clock)
    {
        var name = RoomName.Create(_name).Value;
        var video = VideoSource.Create("https://videos.example.com/watch/1", "video#player", frameOrigin: "").Value;
        var chat = ChatProtection.Public(Convert.ToBase64String(new byte[32])).Value;
        var ownerName = DisplayName.Create(_ownerName).Value;

        var room = Room.Create(name, video, chat, _ownerId, ownerName, _currentTime, clock).Value;
        room.ClearDomainEvents();
        return room;
    }
}
