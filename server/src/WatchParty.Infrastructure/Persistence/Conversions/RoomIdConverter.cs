using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Stores a <see cref="RoomId"/> as a uuid.</summary>
internal sealed class RoomIdConverter() : ValueConverter<RoomId, Guid>(
    roomId => roomId.Value,
    value => new RoomId(value));
