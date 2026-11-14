using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Configurations;

/// <summary>Client-side ciphertext: shared by chat bodies and a private room's verifier.</summary>
internal static class EncryptedTextMapping
{
    public static void Map(ComplexPropertyBuilder<EncryptedText> encrypted)
    {
        encrypted.Property(text => text.Iv).HasMaxLength(EncryptedText.MaxIvLength);
        encrypted.Property(text => text.Ciphertext).HasMaxLength(EncryptedText.MaxCiphertextLength);
    }
}
