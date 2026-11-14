using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Configurations;

/// <summary>
/// One row per room. The value objects are complex properties, so they flatten into
/// columns on this table (<c>video_page_url</c>, <c>playback_is_playing</c>, ...).
/// </summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(room => room.Id);
        builder.MapAggregateRoot();

        builder.Property(room => room.Name).IsRequired();
        builder.Property(room => room.OwnerId).IsRequired();
        builder.Property(room => room.OwnerName).IsRequired();
        builder.Property(room => room.PrimaryOwnerId).IsRequired();
        builder.Property(room => room.PrimaryOwnerName).IsRequired();
        builder.Property(room => room.IsActive).IsRequired();
        builder.Property(room => room.LastActiveAt).IsRequired();
        builder.Property(room => room.CreatedAt).IsRequired();

        // The room list is "newest first", paged by creation time.
        builder.HasIndex(room => room.CreatedAt).IsDescending();

        builder.ComplexProperty(room => room.Video, MapVideo);
        builder.ComplexProperty(room => room.Chat, MapChat);
        builder.ComplexProperty(room => room.Playback, MapPlayback);
        builder.ComplexProperty(room => room.HostPosition, MapHostPosition);
    }

    private static void MapVideo(ComplexPropertyBuilder<VideoSource> video)
    {
        video.Property(source => source.PageUrl).IsRequired().HasMaxLength(VideoSource.MaxUrlLength);
        video.Property(source => source.Selector).IsRequired().HasMaxLength(VideoSource.MaxSelectorLength);
        video.Property(source => source.FrameOrigin).IsRequired().HasMaxLength(VideoSource.MaxOriginLength);
    }

    private static void MapChat(ComplexPropertyBuilder<ChatProtection> chat)
    {
        chat.Property(protection => protection.Visibility).IsRequired();
        chat.Property(protection => protection.ChatKey)
            .HasColumnName("chat_key")
            .IsRequired()
            .HasMaxLength(ChatProtection.MaxKeyMaterialLength);
        chat.Property(protection => protection.ChatSalt)
            .HasColumnName("chat_salt")
            .IsRequired()
            .HasMaxLength(ChatProtection.MaxKeyMaterialLength);
        chat.ComplexProperty(protection => protection.Verifier, EncryptedTextMapping.Map);
    }

    private static void MapPlayback(ComplexPropertyBuilder<PlaybackState> playback)
    {
        playback.Property(state => state.IsPlaying).IsRequired();
        playback.Property(state => state.CurrentTime).IsRequired();
        playback.Property(state => state.Rate).IsRequired();
        playback.Property(state => state.UpdatedBy).IsRequired();
    }

    private static void MapHostPosition(ComplexPropertyBuilder<HostPosition> hostPosition)
    {
        hostPosition.Property(position => position.CurrentTime);
        hostPosition.Property(position => position.IsPlaying);
        hostPosition.Property(position => position.UpdatedAt);
    }
}
