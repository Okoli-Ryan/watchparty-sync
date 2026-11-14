using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Repositories;

namespace WatchParty.Infrastructure.Persistence.Repositories;

internal sealed class ChatMessageRepository(WatchPartyDbContext dbContext) : IChatMessageRepository
{
    public void Add(ChatMessage message) => dbContext.ChatMessages.Add(message);
}
