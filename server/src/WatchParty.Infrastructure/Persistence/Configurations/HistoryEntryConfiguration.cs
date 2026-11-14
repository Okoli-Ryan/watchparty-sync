using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.History;
using WatchParty.Domain.Identity;

namespace WatchParty.Infrastructure.Persistence.Configurations;

/// <summary>
/// Keyed by (user, room). Deliberately no foreign key to rooms: History is its own
/// bounded context and keeps its denormalised copy of the room (<c>room_name</c>, ...).
/// </summary>
internal sealed class HistoryEntryConfiguration : IEntityTypeConfiguration<HistoryEntry>
{
    public void Configure(EntityTypeBuilder<HistoryEntry> builder)
    {
        builder.ToTable("history_entries");
        builder.Ignore(entry => entry.Id);
        builder.HasKey(entry => new { entry.UserId, entry.RoomId });
        builder.MapAggregateRoot();

        builder.Property(entry => entry.IsFavourite).IsRequired();
        builder.ComplexProperty(entry => entry.Room, MapAttendedRoom);

        // "My history, most recently attended first."
        builder.HasIndex(entry => new { entry.UserId, entry.LastAttendedAt }).IsDescending(false, true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void MapAttendedRoom(ComplexPropertyBuilder<AttendedRoom> room)
    {
        room.Property(attended => attended.Name).IsRequired().HasMaxLength(Domain.Rooms.RoomName.MaxLength);
        room.Property(attended => attended.PageUrl).IsRequired().HasMaxLength(Domain.Rooms.VideoSource.MaxUrlLength);
    }
}
