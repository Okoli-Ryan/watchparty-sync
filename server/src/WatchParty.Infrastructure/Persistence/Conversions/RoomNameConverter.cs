using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Reads back through the validating factory, so a corrupt row fails loudly.</summary>
internal sealed class RoomNameConverter() : ValueConverter<RoomName, string>(
    roomName => roomName.Value,
    value => RoomName.Create(value).Value);
