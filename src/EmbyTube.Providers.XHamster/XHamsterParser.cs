using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Providers.XHamster;

/// <summary>
/// Parses xHamster pages. The site embeds its page state as JSON in
/// <c>window.initials = {...};</c>; listings, pagination and video metadata are read
/// from there, searched recursively so moved sub-objects keep working. Written from the
/// live site's markup (2026-10-11). Missing structure raises ParseChanged.
/// </summary>
public static class XHamsterParser
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.Singleline;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

    private static readonly Regex Initials = new(@"window\.initials\s*=\s*(\{.*?\});\s*</script>", Opts, RegexTimeout);
    private static readonly Regex PreloadHls = new(@"<link rel=""preload"" href=""(https://[^""]+\.m3u8[^""]*)""", Opts, RegexTimeout);
    private static readonly Regex VideoPath = new(@"^https://(?:[a-z0-9-]+\.)?xhamster\.com/videos/([A-Za-z0-9_\-]+)$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex CategoryPath = new(@"^https://(?:[a-z0-9-]+\.)?xhamster\.com/categories/([a-z0-9\-]+)$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex MasterResolution = new(@"RESOLUTION=\d+x(\d+)", RegexOptions.CultureInvariant, RegexTimeout);

    private const int MaxDepth = 12;

    public sealed record Listing(IReadOnlyList<TubeVideo> Videos, string? NextPath);

    public static Listing ParseListing(string html, string providerId)
    {
        using var doc = ParseInitials(html);
        var thumbs = Find(doc.RootElement, "videoThumbProps", JsonValueKind.Array)
            ?? throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster listing not found.");

        var videos = new List<TubeVideo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in thumbs.EnumerateArray())
        {
            if (v.ValueKind != JsonValueKind.Object || Bool(v, "isBlockedByGeo"))
                continue;
            var id = VideoIdFromUrl(Str(v, "pageURL"));
            var title = Str(v, "title");
            if (id is null || string.IsNullOrWhiteSpace(title) || !seen.Add(id))
                continue;

            var tags = new List<string>();
            if (Bool(v, "isUHD"))
                tags.Add("4K");
            else if (Bool(v, "isFHD") || Bool(v, "isHD"))
                tags.Add("HD");
            if (Bool(v, "isVR"))
                tags.Add("VR");

            videos.Add(new TubeVideo(providerId, id, WebUtility.HtmlDecode(title).Trim())
            {
                ThumbnailUrl = HttpsUrl(Str(v, "thumbURL")) ?? HttpsUrl(Str(v, "imageURL")),
                Duration = Int(v, "duration") is { } d and > 0 ? TimeSpan.FromSeconds(d) : null,
                PublishedAt = Long(v, "created") is { } c and > 0 ? DateTimeOffset.FromUnixTimeSeconds(c) : null,
                SourcePageUrl = XHamsterProvider.BaseUrl + "videos/" + id,
                Tags = tags,
            });
        }

        return new Listing(videos, NextPath(doc.RootElement));
    }

    /// <summary>Main content categories from the home page sidebar (the /categories page is mostly countries).</summary>
    public static IReadOnlyList<TubeCategory> ParseCategories(string html)
    {
        using var doc = ParseInitials(html);
        var items = Find(doc.RootElement, "sidebarProps", JsonValueKind.Object) is { } sidebar
            && sidebar.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr
                : throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster categories not found.");

        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var url = Str(item, "url");
            var name = Str(item, "name");
            if (url is null || string.IsNullOrWhiteSpace(name))
                continue;
            var m = CategoryPath.Match(url);
            if (m.Success && seen.Add(m.Groups[1].Value))
                cats.Add(new TubeCategory(m.Groups[1].Value, WebUtility.HtmlDecode(name).Trim()));
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster categories not found.");
        return cats;
    }

    public sealed record VideoPage(TubeVideo? Video, Uri? Hls, bool Deleted);

    public static VideoPage ParseVideoPage(string html, string providerId, string videoId)
    {
        if (html.Contains("This video was deleted", StringComparison.Ordinal)
            || html.Contains("Video is not available", StringComparison.Ordinal))
            return new VideoPage(null, null, Deleted: true);

        // Only the plain HLS URL the page itself preloads is used. The player config also
        // carries obfuscated URLs; those are deliberately not decoded.
        var hls = PreloadHls.Match(html) is { Success: true } p
            && Uri.TryCreate(WebUtility.HtmlDecode(p.Groups[1].Value), UriKind.Absolute, out var u) ? u : null;

        TubeVideo? video = null;
        if (Initials.IsMatch(html))
        {
            using var doc = ParseInitials(html);
            if (Find(doc.RootElement, "videoModel", JsonValueKind.Object) is { } vm && Str(vm, "title") is { } title)
            {
                video = new TubeVideo(providerId, videoId, WebUtility.HtmlDecode(title).Trim())
                {
                    Description = Str(vm, "description") is { Length: > 0 } desc ? WebUtility.HtmlDecode(desc).Trim() : null,
                    Duration = Int(vm, "duration") is { } d and > 0 ? TimeSpan.FromSeconds(d) : null,
                    PublishedAt = Long(vm, "created") is { } c and > 0 ? DateTimeOffset.FromUnixTimeSeconds(c) : null,
                    ThumbnailUrl = HttpsUrl(Str(vm, "thumbURL")),
                    SourcePageUrl = XHamsterProvider.BaseUrl + "videos/" + videoId,
                };
            }
        }

        if (video is null && hls is null)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster video page layout not recognized.");

        return new VideoPage(video, hls, Deleted: false);
    }

    public static int? MaxHeight(string masterPlaylist)
    {
        int? max = null;
        foreach (Match m in MasterResolution.Matches(masterPlaylist))
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var h) && h > (max ?? 0))
                max = h;
        }

        return max;
    }

    /// <summary>"…/videos/some-title-xh1AbC2" → "some-title-xh1AbC2".</summary>
    public static string? VideoIdFromUrl(string? url) =>
        url is not null && VideoPath.Match(url) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Site-relative path only; absolute xhamster.com URLs are reduced to their path.</summary>
    public static string? NormalizePath(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (value.StartsWith("https://xhamster.com/", StringComparison.Ordinal))
            value = value["https://xhamster.com".Length..];
        if (value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal) || value.Contains('\\') || value.Contains('{'))
            return null;
        return value;
    }

    private static string? NextPath(JsonElement root)
    {
        // Listing pages: paginationProps {currentPageNumber, lastPageNumber, pageLinkTemplate}
        if (Find(root, "paginationProps", JsonValueKind.Object) is { } pp
            && Int(pp, "currentPageNumber") is { } cur && Int(pp, "lastPageNumber") is { } last
            && Str(pp, "pageLinkTemplate") is { } tpl1)
            return cur < last ? NormalizePath(tpl1.Replace("{#}", (cur + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) : null;

        // Search pages: pagination {next, maxPages, pageLinkTemplate}
        if (Find(root, "pagination", JsonValueKind.Object) is { } pg
            && Int(pg, "next") is { } next && Str(pg, "pageLinkTemplate") is { } tpl2)
        {
            var max = Int(pg, "maxPages") ?? Int(pg, "maxPage") ?? next;
            return next <= max && next > 1 ? NormalizePath(tpl2.Replace("{#}", next.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) : null;
        }

        return null;
    }

    private static JsonDocument ParseInitials(string html)
    {
        var m = Initials.Match(html);
        if (!m.Success)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster page state not found.");
        try
        {
            return JsonDocument.Parse(m.Groups[1].Value, new JsonDocumentOptions { MaxDepth = 128 });
        }
        catch (JsonException ex)
        {
            throw new ProviderException(ProviderErrorKind.ParseChanged, "xHamster page state is not valid JSON.", ex);
        }
    }

    /// <summary>Depth-first search for the first property with this name and kind.</summary>
    private static JsonElement? Find(JsonElement e, string name, JsonValueKind kind, int depth = 0)
    {
        if (depth > MaxDepth)
            return null;
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty(name, out var hit) && hit.ValueKind == kind)
                return hit;
            foreach (var p in e.EnumerateObject())
            {
                if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && Find(p.Value, name, kind, depth + 1) is { } r)
                    return r;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && Find(item, name, kind, depth + 1) is { } r)
                    return r;
            }
        }

        return null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i) ? i : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? HttpsUrl(string? s) =>
        s is not null && s.StartsWith("https://", StringComparison.Ordinal) ? s : null;
}
