using WatchParty.Application.Abstractions.Persistence;
using WatchParty.Domain.History;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.UnitTests.Fakes;

/// <summary>
/// In-memory repositories and unit of work for handler tests. Writes land immediately
/// and concurrency is not modelled: that belongs to the integration tests, which run
/// against a real database.
/// </summary>
internal sealed class InMemoryStore :
    IUserRepository,
    IRoomRepository,
    IMembershipRepository,
    IChatMessageRepository,
    IHistoryEntryRepository,
    IUnitOfWork
{
    public Dictionary<UserId, User> Users { get; } = [];

    public Dictionary<RoomId, Room> Rooms { get; } = [];

    public Dictionary<MembershipId, Membership> Memberships { get; } = [];

    public List<ChatMessage> Messages { get; } = [];

    public Dictionary<HistoryEntryId, HistoryEntry> History { get; } = [];

    public int Commits { get; private set; }

    public Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        Commits++;
        return Task.FromResult(Result.Success());
    }

    Task<User?> IUserRepository.GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.GetValueOrDefault(id));

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var user = Users.Values.FirstOrDefault(candidate => candidate.Email == email);
        return Task.FromResult(user);
    }

    public void Add(User user) => Users[user.Id] = user;

    public void Update(User user) => Users[user.Id] = user;

    Task<Room?> IRoomRepository.GetByIdAsync(RoomId id, CancellationToken cancellationToken) =>
        Task.FromResult(Rooms.GetValueOrDefault(id));

    public void Add(Room room) => Rooms[room.Id] = room;

    public void Update(Room room) => Rooms[room.Id] = room;

    Task<Membership?> IMembershipRepository.GetAsync(MembershipId id, CancellationToken cancellationToken) =>
        Task.FromResult(Memberships.GetValueOrDefault(id));

    public Task<IReadOnlyList<Membership>> ListByRoomAsync(RoomId roomId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> members = Memberships.Values
            .Where(membership => membership.RoomId == roomId)
            .ToList();

        return Task.FromResult(members);
    }

    public void Add(Membership membership) => Memberships[membership.Id] = membership;

    public void Update(Membership membership) => Memberships[membership.Id] = membership;

    public void Remove(Membership membership) => Memberships.Remove(membership.Id);

    public void Add(ChatMessage message) => Messages.Add(message);

    Task<HistoryEntry?> IHistoryEntryRepository.GetAsync(HistoryEntryId id, CancellationToken cancellationToken) =>
        Task.FromResult(History.GetValueOrDefault(id));

    public void Add(HistoryEntry entry) => History[entry.Id] = entry;

    public void Update(HistoryEntry entry) => History[entry.Id] = entry;

    public void Remove(HistoryEntry entry) => History.Remove(entry.Id);
}
