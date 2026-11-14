using System.Buffers.Text;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// AES-GCM output produced by the CLIENT. The server stores and relays it but can
/// never read it: keys are generated, or derived from a passphrase, in the browser.
/// </summary>
public sealed record EncryptedText
{
    public const int MaxIvLength = 64;

    /// <summary>Room for the client's 500-character limit as UTF-8, plus the GCM tag, as base64.</summary>
    public const int MaxCiphertextLength = 4096;

    public static readonly Error Invalid =
        Error.Validation("Chat.InvalidCiphertext", "The encrypted payload is malformed or too large.");

#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private EncryptedText()
    {
    }
#pragma warning restore CS8618

    private EncryptedText(string iv, string ciphertext)
    {
        Iv = iv;
        Ciphertext = ciphertext;
    }

    public string Iv { get; private init; }

    public string Ciphertext { get; private init; }

    public static Result<EncryptedText> Create(string? iv, string? ciphertext)
    {
        if (!IsBase64(iv, MaxIvLength) || !IsBase64(ciphertext, MaxCiphertextLength))
        {
            return Invalid;
        }

        return new EncryptedText(iv!, ciphertext!);
    }

    internal static bool IsBase64(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length <= maxLength && Base64.IsValid(value);
}
