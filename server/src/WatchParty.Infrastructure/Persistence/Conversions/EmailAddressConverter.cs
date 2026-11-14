using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WatchParty.Domain.Identity;

namespace WatchParty.Infrastructure.Persistence.Conversions;

/// <summary>Stored already normalised, so the unique index compares like with like.</summary>
internal sealed class EmailAddressConverter() : ValueConverter<EmailAddress, string>(
    email => email.Value,
    value => EmailAddress.Create(value).Value);
