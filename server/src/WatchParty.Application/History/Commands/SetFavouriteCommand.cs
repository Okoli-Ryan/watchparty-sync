using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands;

public sealed record SetFavouriteCommand(UserId ActorId, RoomId RoomId, bool Favourite);
