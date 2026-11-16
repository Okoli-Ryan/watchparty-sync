using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.History.Commands.SetFavourite;

public sealed record SetFavouriteCommand(UserId ActorId, RoomId RoomId, bool Favourite);
