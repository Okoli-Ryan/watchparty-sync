using WatchParty.Domain.Identity;
using WatchParty.Domain.Rooms;

namespace WatchParty.Domain.UnitTests.Rooms;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example.com/movie")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void VideoSource_WithNonHttpPageUrl_IsRejected(string pageUrl)
    {
        var result = VideoSource.Create(pageUrl, "video", frameOrigin: "");

        result.Error.ShouldBe(RoomErrors.InvalidPageUrl);
    }

    [Theory]
    [InlineData("https://player.example.com/embed/1")]
    [InlineData("not a url")]
    public void VideoSource_WithFrameOriginThatIsNotAnOrigin_IsRejected(string frameOrigin)
    {
        var result = VideoSource.Create("https://example.com/watch", "video", frameOrigin);

        result.Error.ShouldBe(RoomErrors.InvalidFrameOrigin);
    }

    [Fact]
    public void VideoSource_WithEmptyFrameOrigin_MeansTheTopDocument()
    {
        var result = VideoSource.Create("https://example.com/watch", "video", frameOrigin: null);

        result.Value.FrameOrigin.ShouldBeEmpty();
    }

    [Fact]
    public void EmailAddress_IsTrimmedAndLowerCased()
    {
        var result = EmailAddress.Create("  Someone@Example.COM ");

        result.Value.Value.ShouldBe("someone@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("Name <someone@example.com>")]
    public void EmailAddress_WhenMalformed_IsRejected(string email)
    {
        EmailAddress.Create(email).Error.ShouldBe(UserErrors.InvalidEmail);
    }

    [Fact]
    public void ChatProtection_PublicRoomWithoutKey_IsRejected()
    {
        ChatProtection.Public(chatKey: "").Error.ShouldBe(RoomErrors.InvalidChatKeyMaterial);
    }

    [Fact]
    public void ChatProtection_PrivateRoomWithoutVerifier_IsRejected()
    {
        var salt = Convert.ToBase64String(new byte[16]);

        ChatProtection.Private(salt, verifier: null).Error.ShouldBe(RoomErrors.InvalidChatKeyMaterial);
    }

    [Fact]
    public void ChatProtection_PrivateRoom_StoresNoKey()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var verifier = EncryptedText.Create(Convert.ToBase64String(new byte[12]), Convert.ToBase64String(new byte[32])).Value;

        var protection = ChatProtection.Private(salt, verifier).Value;

        protection.ChatKey.ShouldBeEmpty();
        protection.Visibility.ShouldBe(RoomVisibility.Private);
    }

    [Theory]
    [InlineData("not base64!", "AAAA")]
    [InlineData("AAAA", "")]
    public void EncryptedText_WhenNotBase64_IsRejected(string initializationVector, string ciphertext)
    {
        EncryptedText.Create(initializationVector, ciphertext).Error.ShouldBe(EncryptedText.Invalid);
    }

    [Fact]
    public void EncryptedText_LargerThanAnyRealMessage_IsRejected()
    {
        var oversized = Convert.ToBase64String(new byte[EncryptedText.MaxCiphertextLength]);

        EncryptedText.Create("AAAAAAAAAAAAAAAA", oversized).Error.ShouldBe(EncryptedText.Invalid);
    }
}
