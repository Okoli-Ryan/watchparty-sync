using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Identity;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.MapAggregateRoot();

        builder.Property(user => user.Email).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.DisplayName).IsRequired();
        builder.Property(user => user.Role).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Ignore(user => user.IsAdmin);
    }
}
