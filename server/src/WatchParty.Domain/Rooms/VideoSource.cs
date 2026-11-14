using WatchParty.Domain.SharedKernel;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// Where the room's video lives: the top-level page a viewer navigates to, the CSS
/// selector of the video element, and the origin of the frame holding it (empty means
/// the top document).
/// </summary>
public sealed record VideoSource
{
    public const int MaxUrlLength = 4096;

    public const int MaxSelectorLength = 4096;

    public const int MaxOriginLength = 512;

#pragma warning disable CS8618 // EF Core materialises through this constructor and sets every property.
    private VideoSource()
    {
    }
#pragma warning restore CS8618

    private VideoSource(string pageUrl, string selector, string frameOrigin)
    {
        PageUrl = pageUrl;
        Selector = selector;
        FrameOrigin = frameOrigin;
    }

    public string PageUrl { get; private init; }

    public string Selector { get; private init; }

    public string FrameOrigin { get; private init; }

    public static Result<VideoSource> Create(string? pageUrl, string? selector, string? frameOrigin)
    {
        if (!TryParseHttpUrl(pageUrl, MaxUrlLength, out _))
        {
            return RoomErrors.InvalidPageUrl;
        }

        if (string.IsNullOrWhiteSpace(selector) || selector.Length > MaxSelectorLength)
        {
            return RoomErrors.InvalidSelector;
        }

        var origin = frameOrigin?.Trim() ?? string.Empty;
        if (origin.Length > 0
            && (!TryParseHttpUrl(origin, MaxOriginLength, out var parsed)
                || !string.Equals(parsed.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase)))
        {
            return RoomErrors.InvalidFrameOrigin;
        }

        return new VideoSource(pageUrl!, selector, origin);
    }

    private static bool TryParseHttpUrl(string? value, int maxLength, out Uri uri)
    {
        uri = null!;
        if (value is not { Length: > 0 } || value.Length > maxLength
            || !Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        uri = parsed;
        return parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps;
    }
}
