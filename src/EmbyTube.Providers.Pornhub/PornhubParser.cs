using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Providers.Pornhub;

/// <summary>
/// Parses Pornhub pages, written from the live markup (2026-10-11). Listing pages carry
/// several video lists (hottest, recommended, premium…); only the main result list
/// (<c>ul#videoCategory</c> / <c>ul#videoSearchResult</c>) is read. Premium-only items are
/// skipped. Missing structure raises ParseChanged.
/// </summary>
public static class PornhubParser
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

    private static readonly Regex MainList = new(@"<ul\b[^>]*\bid=""(?:videoCategory|videoSearchResult|mostRecentVideosSection|categoryVideos)""[^>]*>", Opts, RegexTimeout);
    private static readonly Regex OtherList = new(@"<ul\b[^>]*\bclass=""[^""]*\bvideos\b[^""]*""", Opts, RegexTimeout);
    private static readonly Regex Item = new(@"<li\b[^>]*class=""pcVideoListItem[^""]*""[^>]*data-video-vkey=""([a-z0-9]+)""", Opts, RegexTimeout);
    private static readonly Regex TitleAttr = new(@"\btitle=""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex Thumb = new(@"\bdata-mediumthumb=""(https://[^""]+)""", Opts, RegexTimeout);
    private static readonly Regex Duration = new(@"<var class=""duration"">\s*([\d:]+)\s*</var>", Opts, RegexTimeout);
    private static readonly Regex NextPage = new(@"<li\s+class=""page_next"">\s*<a\s+href=""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex CategoryWrapper = new(@"<div class=""category-wrapper[^""]*"">", Opts, RegexTimeout);
    private static readonly Regex CategoryLink = new(@"<a\s+href=""/(?:video\?c=(\d+)|categories/([a-z0-9\-]+))""\s+alt=""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex OgTitle = new(@"<meta property=""og:title"" content=""([^""]*)""", Opts, RegexTimeout);
    private static readonly Regex UploadDate = new(@"""uploadDate""\s*:\s*""([^""]+)""", Opts, RegexTimeout);

    public sealed record Listing(IReadOnlyList<TubeVideo> Videos, string? NextPath);

    public static Listing ParseListing(string html, string providerId)
    {
        var main = MainList.Match(html);
        if (!main.Success)
        {
            if (html.Contains("No results found", StringComparison.OrdinalIgnoreCase)
                || html.Contains("noResultsWrapper", StringComparison.OrdinalIgnoreCase))
                return new Listing(Array.Empty<TubeVideo>(), null);
            throw new ProviderException(ProviderErrorKind.ParseChanged, "Pornhub listing not found.");
        }

        var start = main.Index + main.Length;
        var other = OtherList.Match(html, start);
        var section = html[start..(other.Success ? other.Index : html.Length)];

        var items = Item.Matches(section);
        var videos = new List<TubeVideo>(items.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var end = i + 1 < items.Count ? items[i + 1].Index : section.Length;
            var block = section[items[i].Index..end];
            var key = items[i].Groups[1].Value;
            if (block.Contains("premiumIcon", StringComparison.Ordinal) || !seen.Add(key))
                continue;

            var title = TitleAttr.Match(block) is { Success: true } t ? WebUtility.HtmlDecode(t.Groups[1].Value).Trim() : null;
            if (string.IsNullOrEmpty(title))
                continue;

            videos.Add(new TubeVideo(providerId, key, title)
            {
                ThumbnailUrl = Thumb.Match(block) is { Success: true } th ? WebUtility.HtmlDecode(th.Groups[1].Value) : null,
                Duration = Duration.Match(block) is { Success: true } d ? ParseClock(d.Groups[1].Value) : null,
                SourcePageUrl = PornhubProvider.BaseUrl + "view_video.php?viewkey=" + key,
            });
        }

        var next = NextPage.Match(html) is { Success: true } n ? NormalizePath(WebUtility.HtmlDecode(n.Groups[1].Value)) : null;
        return new Listing(videos, next);
    }

    public static IReadOnlyList<TubeCategory> ParseCategories(string html)
    {
        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var wrappers = CategoryWrapper.Matches(html);
        for (var i = 0; i < wrappers.Count; i++)
        {
            // Only the wrapper's own first link; section links (e.g. "/gayporn") are skipped.
            var end = i + 1 < wrappers.Count ? wrappers[i + 1].Index : Math.Min(html.Length, wrappers[i].Index + 2_000);
            var m = CategoryLink.Match(html[wrappers[i].Index..end]);
            if (!m.Success)
                continue;
            var id = m.Groups[1].Success ? "c" + m.Groups[1].Value : m.Groups[2].Value;
            if (seen.Add(id))
                cats.Add(new TubeCategory(id, WebUtility.HtmlDecode(m.Groups[3].Value).Trim()));
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "Pornhub categories not found.");
        return cats;
    }

    public sealed record Stream(Uri Url, int? Height);

    public sealed record VideoPage(TubeVideo? Video, IReadOnlyList<Stream> Hls);

    /// <summary>Reads <c>flashvars.mediaDefinitions</c> HLS entries and basic metadata.</summary>
    public static VideoPage ParseVideoPage(string html, string providerId, string viewKey)
    {
        var streams = new List<Stream>();
        var idx = html.IndexOf("\"mediaDefinitions\"", StringComparison.Ordinal);
        if (idx >= 0 && html.IndexOf('[', idx) is var arr and >= 0)
        {
            try
            {
                var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(html[arr..Math.Min(html.Length, arr + 200_000)]));
                using var doc = JsonDocument.ParseValue(ref reader);
                foreach (var d in doc.RootElement.EnumerateArray())
                {
                    if (Str(d, "format") != "hls" || Str(d, "videoUrl") is not { } url
                        || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                        continue;
                    int? height = d.TryGetProperty("quality", out var q) && q.ValueKind == JsonValueKind.String
                        && int.TryParse(q.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var h) ? h : null;
                    streams.Add(new Stream(uri, height));
                }
            }
            catch (JsonException ex)
            {
                throw new ProviderException(ProviderErrorKind.ParseChanged, "Pornhub media definitions are not valid JSON.", ex);
            }
        }

        var title = ScriptString(html, "video_title") ?? (OgTitle.Match(html) is { Success: true } t ? t.Groups[1].Value : null);
        TubeVideo? video = null;
        if (!string.IsNullOrWhiteSpace(title))
        {
            video = new TubeVideo(providerId, viewKey, WebUtility.HtmlDecode(title).Trim())
            {
                Duration = ScriptNumber(html, "video_duration") is { } secs and > 0 ? TimeSpan.FromSeconds(secs) : null,
                ThumbnailUrl = ScriptString(html, "image_url") is { } img && img.StartsWith("https://", StringComparison.Ordinal) ? img : null,
                PublishedAt = UploadDate.Match(html) is { Success: true } u
                    && DateTimeOffset.TryParse(u.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p) ? p : null,
                SourcePageUrl = PornhubProvider.BaseUrl + "view_video.php?viewkey=" + viewKey,
            };
        }

        if (video is null && streams.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "Pornhub video page layout not recognized.");
        return new VideoPage(video, streams);
    }

    public static TimeSpan? ParseClock(string text)
    {
        var parts = text.Split(':');
        if (parts.Length is < 2 or > 3)
            return null;
        var total = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                return null;
            total = (total * 60) + n;
        }

        return TimeSpan.FromSeconds(total);
    }

    /// <summary>Site-relative paths only ("/video?o=cm&amp;page=2").</summary>
    public static string? NormalizePath(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (value.StartsWith("https://www.pornhub.com/", StringComparison.Ordinal))
            value = value["https://www.pornhub.com".Length..];
        return value[0] == '/' && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') ? value : null;
    }

    // flashvars keys look like "video_title":"…" / "video_duration":123 (JSON-escaped).
    private static string? ScriptString(string html, string key)
    {
        var m = Regex.Match(html, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant, RegexTimeout);
        if (!m.Success)
            return null;
        try
        {
            return JsonSerializer.Deserialize<string>("\"" + m.Groups[1].Value + "\"");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ScriptNumber(string html, string key)
    {
        var m = Regex.Match(html, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"?(\\d+)", RegexOptions.CultureInvariant, RegexTimeout);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
