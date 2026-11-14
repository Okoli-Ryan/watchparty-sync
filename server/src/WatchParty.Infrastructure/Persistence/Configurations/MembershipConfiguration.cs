using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Configurations;

/// <summary>
/// Keyed by (room, user). The aggregate's <c>Id</c> is derived from those two columns,
/// so it is not mapped itself. Rooms and users are referenced by id only (§2.3).
/// </summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.Ignore(membership => membership.Id);
        builder.HasKey(membership => new { membership.RoomId, membership.UserId });
        builder.MapAggregateRoot();

        builder.Property(membership => membership.DisplayName).IsRequired();
        builder.Property(membership => membership.Role).IsRequired();
        builder.Property(membership => membership.JoinedAt).IsRequired();
        builder.Property(membership => membership.LastSeen).IsRequired();

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(membership => membership.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
