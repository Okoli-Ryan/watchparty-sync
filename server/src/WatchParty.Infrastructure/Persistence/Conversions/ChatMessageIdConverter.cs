using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Stores a <see cref="ChatMessageId"/> as a uuid; version 7, so it sorts by time.</summary>
internal sealed class ChatMessageIdConverter() : ValueConverter<ChatMessageId, Guid>(
    messageId => messageId.Value,
    value => new ChatMessageId(value));
