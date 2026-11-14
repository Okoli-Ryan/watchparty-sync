using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Reads back through the validating factory, so a corrupt row fails loudly.</summary>
internal sealed class DisplayNameConverter() : ValueConverter<DisplayName, string>(
    displayName => displayName.Value,
    value => DisplayName.Create(value).Value);
