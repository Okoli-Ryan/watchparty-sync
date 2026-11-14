using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Stores a <see cref="UserId"/> as a uuid.</summary>
internal sealed class UserIdConverter() : ValueConverter<UserId, Guid>(
    userId => userId.Value,
    value => new UserId(value));
