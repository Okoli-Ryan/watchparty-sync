using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.History;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;
using WatchParty.Infrastructure.Persistence.Conversions;

namespace WatchParty.Infrastructure.Persistence;

/// <summary>
/// The single unit of work for WatchParty, over Postgres (Neon in production). The domain
/// knows nothing of it: every mapping lives in <c>Configurations/</c>.
/// </summary>
public sealed class WatchPartyDbContext(DbContextOptions<WatchPartyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<HistoryEntry> HistoryEntries => Set<HistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WatchPartyDbContext).Assembly);
    }

    /// <summary>
    /// Conversions that hold wherever a type appears, including inside value objects:
    /// strongly-typed ids become uuid columns, single-value value objects become text,
    /// and enums are stored by name (§2.5).
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<UserId>().HaveConversion<UserIdConverter>();
        configurationBuilder.Properties<RoomId>().HaveConversion<RoomIdConverter>();
        configurationBuilder.Properties<ChatMessageId>().HaveConversion<ChatMessageIdConverter>();

        configurationBuilder.Properties<DisplayName>()
            .HaveConversion<DisplayNameConverter>()
            .HaveMaxLength(DisplayName.MaxLength);

        configurationBuilder.Properties<RoomName>()
            .HaveConversion<RoomNameConverter>()
            .HaveMaxLength(RoomName.MaxLength);

        configurationBuilder.Properties<EmailAddress>()
            .HaveConversion<EmailAddressConverter>()
            .HaveMaxLength(EmailAddress.MaxLength);

        configurationBuilder.Properties<Role>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<MemberRole>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<RoomVisibility>().HaveConversion<string>().HaveMaxLength(16);
    }
}
