using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(message => message.Id);
        builder.MapAggregateRoot();

        builder.Property(message => message.RoomId).IsRequired();
        builder.Property(message => message.SenderId).IsRequired();
        builder.Property(message => message.SenderName).IsRequired();
        builder.Property(message => message.SentAt).IsRequired();
        builder.ComplexProperty(message => message.Body, EncryptedTextMapping.Map);

        // A transcript is "the newest N messages of one room".
        builder.HasIndex(message => new { message.RoomId, message.SentAt }).IsDescending(false, true);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(message => message.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
