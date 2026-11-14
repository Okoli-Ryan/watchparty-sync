using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// How a room's chat key is held (DECISIONS.md #13).
/// Public: a random key is stored with the room, so joining is one click.
/// Private: the key is derived from a passphrase that is never sent to the server;
/// only the PBKDF2 salt and a verifier (known plaintext encrypted with the key) are stored.
/// </summary>
public sealed record ChatProtection
{
    public const int MaxKeyMaterialLength = 128;

#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private ChatProtection()
    {
    }
#pragma warning restore CS8618

    private ChatProtection(RoomVisibility visibility, string chatKey, string chatSalt, EncryptedText? verifier)
    {
        Visibility = visibility;
        ChatKey = chatKey;
        ChatSalt = chatSalt;
        Verifier = verifier;
    }

    public RoomVisibility Visibility { get; private init; }

    /// <summary>Base64 AES key. Public rooms only, otherwise empty.</summary>
    public string ChatKey { get; private init; }

    /// <summary>Base64 PBKDF2 salt. Private rooms only, otherwise empty.</summary>
    public string ChatSalt { get; private init; }

    /// <summary>Lets a client check a passphrase. Private rooms only.</summary>
    public EncryptedText? Verifier { get; private init; }

    public static Result<ChatProtection> Public(string? chatKey) =>
        EncryptedText.IsBase64(chatKey, MaxKeyMaterialLength)
            ? new ChatProtection(RoomVisibility.Public, chatKey!, string.Empty, verifier: null)
            : RoomErrors.InvalidChatKeyMaterial;

    public static Result<ChatProtection> Private(string? chatSalt, EncryptedText? verifier) =>
        EncryptedText.IsBase64(chatSalt, MaxKeyMaterialLength) && verifier is not null
            ? new ChatProtection(RoomVisibility.Private, string.Empty, chatSalt!, verifier)
            : RoomErrors.InvalidChatKeyMaterial;
}
