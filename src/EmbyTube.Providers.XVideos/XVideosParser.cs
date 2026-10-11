using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Providers.XVideos;

/// <summary>Per-site differences within the XVideos platform family (XVideos, XNXX).</summary>
public sealed record XvSite(string Name, string BaseUrl, string VideoPathPrefix, Regex VideoHref)
{
    /// <summary>/video.&lt;eid&gt;/&lt;slug&gt; → id "eid/slug".</summary>
    public static readonly XvSite XVideos = new(
        "XVideos", "https://www.xvideos.com/", "video.",
        new Regex(@"href=""/video\.([a-z0-9]+/[^""/?#\s]+)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)));

    /// <summary>/video-&lt;eid&gt;/…/&lt;slug&gt; → id "eid/…/slug" (the site 404s without the full path).</summary>
    public static readonly XvSite Xnxx = new(
        "XNXX", "https://www.xnxx.com/", "video-",
        new Regex(@"href=""/video-([a-z0-9]+(?:/[A-Za-z0-9_\-\.]+)+)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)));
}

/// <summary>
/// Parses XVideos-platform HTML (XVideos and XNXX share markup and player). Written from
/// the sites' markup as observed on 2026-10-11 (two listing layouts: <c>div id="video_…"</c>
/// and <c>div id="video-thumb-…"</c>).
/// Every method throws <see cref="ProviderException"/> (ParseChanged) when the expected
/// structure is missing, instead of silently returning nothing.
/// </summary>
public static class XVideosParser
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private static readonly Regex BlockStart = new(@"<div\s+id=""video(?:_|-thumb-)([a-z0-9]+)""", Opts, RegexTimeout);
    private static readonly Regex TitleAttr = new(@"\btitle=""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex DataSrc = new(@"\bdata-src=""(https://[^""]+)""", Opts, RegexTimeout);
    private static readonly Regex SfwThumb = new(@"\bdata-sfwthumb=""(https://[^""]+)""", Opts, RegexTimeout);
    private static readonly Regex DurationSpan = new(@"<span class=""duration"">([^<]+)</span>", Opts, RegexTimeout);
    // XNXX: "… 86%</span></span> 10min <span class="video-hd">" or "<span class="left"> 13min <span …>".
    private static readonly Regex DurationText = new(@">\s*(\d+\s*(?:h|min|sec)(?:\s*\d+\s*(?:min|sec))?)\s*<span", Opts, RegexTimeout);
    private static readonly Regex HdMark = new(@"video-hd(?:-mark)?"">(?:\s*<span[^>]*>[^<]*</span>)?\s*(\d{3,4})p", Opts, RegexTimeout);
    private static readonly Regex NextPage = new(@"href=""([^""]+)""\s+class=""no-page next-page", Opts, RegexTimeout);

    // XNXX-style pagination: the link after the active/current page.
    private static readonly Regex NextAfterActive = new(@"class=""(?:active|current)""[^>]*>[^<]*</a>\s*(?:</li>\s*<li[^>]*>\s*)?<a[^>]*\bhref=""(/[^""]+)""", Opts, RegexTimeout);
    private static readonly Regex CategoryLink = new(@"href=""/c/([A-Za-z0-9_]+-\d+)""[^>]*>\s*([^<]+?)\s*<", Opts, RegexTimeout);
    private static readonly Regex DurationPart = new(@"(\d+)\s*(h|min|sec)", Opts, RegexTimeout);

    private static readonly Regex HlsUrl = new(@"setVideoHLS\('(https://[^']+)'", Opts, RegexTimeout);
    private static readonly Regex Mp4High = new(@"setVideoUrlHigh\('(https://[^']+)'", Opts, RegexTimeout);
    private static readonly Regex Mp4Low = new(@"setVideoUrlLow\('(https://[^']+)'", Opts, RegexTimeout);
    private static readonly Regex OgTitle = new(@"<meta property=""og:title"" content=""([^""]*)""", Opts, RegexTimeout);
    private static readonly Regex OgDuration = new(@"<meta property=""og:duration"" content=""(\d+)""", Opts, RegexTimeout);
    private static readonly Regex Thumb169 = new(@"setThumbUrl169\('(https://[^']+)'", Opts, RegexTimeout);
    private static readonly Regex UploadDate = new(@"""uploadDate""\s*:\s*""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex MasterResolution = new(@"RESOLUTION=\d+x(\d+)", Opts, RegexTimeout);

    public sealed record Listing(IReadOnlyList<TubeVideo> Videos, string? NextPath);

    public static Listing ParseListing(string html, string providerId, bool sfwThumbnails) =>
        ParseListing(html, providerId, sfwThumbnails, XvSite.XVideos);

    public static Listing ParseListing(string html, string providerId, bool sfwThumbnails, XvSite site)
    {
        var starts = BlockStart.Matches(html);
        var videos = new List<TubeVideo>(starts.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].Index : Math.Min(html.Length, starts[i].Index + 20_000);
            var block = html.Substring(starts[i].Index, end - starts[i].Index);

            var href = site.VideoHref.Match(block);
            if (!href.Success)
                continue;

            var id = href.Groups[1].Value;
            if (!seen.Add(id))
                continue;

            var slug = id[(id.LastIndexOf('/') + 1)..];
            var title = TitleAttr.Match(block) is { Success: true } t ? Decode(t.Groups[1].Value) : Decode(slug.Replace('_', ' '));
            var thumb = (sfwThumbnails ? SfwThumb.Match(block) : DataSrc.Match(block)) is { Success: true } th
                ? WebUtility.HtmlDecode(th.Groups[1].Value)
                : null;

            var tags = new List<string>();
            if (HdMark.Match(block) is { Success: true } hd)
                tags.Add(hd.Groups[1].Value + "p");

            videos.Add(new TubeVideo(providerId, id, title)
            {
                ThumbnailUrl = thumb,
                Duration = DurationSpan.Match(block) is { Success: true } d ? ParseDuration(d.Groups[1].Value)
                    : DurationText.Match(block) is { Success: true } d2 ? ParseDuration(d2.Groups[1].Value)
                    : null,
                SourcePageUrl = site.BaseUrl + site.VideoPathPrefix + id,
                Tags = tags,
            });
        }

        if (videos.Count == 0 && !IsEmptyResult(html))
            throw new ProviderException(ProviderErrorKind.ParseChanged, $"{site.Name} listing layout not recognized.");

        var next = NextPage.Match(html) is { Success: true } n ? NormalizePath(WebUtility.HtmlDecode(n.Groups[1].Value))
            : NextAfterActive.Match(html) is { Success: true } a ? NormalizePath(WebUtility.HtmlDecode(a.Groups[1].Value))
            : null;
        return new Listing(videos, next);
    }

    public static IReadOnlyList<TubeCategory> ParseCategories(string html)
    {
        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in CategoryLink.Matches(html))
        {
            var name = Decode(m.Groups[2].Value);
            if (name.Length == 0 || !seen.Add(m.Groups[1].Value))
                continue;
            cats.Add(new TubeCategory(m.Groups[1].Value, name));
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "XVideos categories not found.");

        return cats.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public sealed record VideoPage(TubeVideo Video, Uri? Hls, Uri? Mp4High, Uri? Mp4Low);

    public static VideoPage ParseVideoPage(string html, string providerId, string videoId) =>
        ParseVideoPage(html, providerId, videoId, XvSite.XVideos);

    public static VideoPage ParseVideoPage(string html, string providerId, string videoId, XvSite site)
    {
        var hls = AbsUrl(HlsUrl.Match(html));
        var high = AbsUrl(Mp4High.Match(html));
        var low = AbsUrl(Mp4Low.Match(html));

        DateTimeOffset? published = UploadDate.Match(html) is { Success: true } u
            && DateTimeOffset.TryParse(u.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p)
                ? p
                : null;

        var title = OgTitle.Match(html) is { Success: true } t ? Decode(t.Groups[1].Value) : null;
        if (title is null && hls is null && high is null && low is null)
            throw new ProviderException(ProviderErrorKind.ParseChanged, $"{site.Name} video page layout not recognized.");

        var video = new TubeVideo(providerId, videoId, title ?? videoId)
        {
            Duration = OgDuration.Match(html) is { Success: true } d
                && int.TryParse(d.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var secs)
                    ? TimeSpan.FromSeconds(secs)
                    : null,
            ThumbnailUrl = Thumb169.Match(html) is { Success: true } th ? th.Groups[1].Value : null,
            PublishedAt = published,
            SourcePageUrl = site.BaseUrl + site.VideoPathPrefix + videoId,
        };
        return new VideoPage(video, hls, high, low);
    }

    /// <summary>Highest rendition height in an HLS master playlist, if any.</summary>
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

    /// <summary>Parses "10 min", "45 sec", "1 h 5 min". Unknown formats yield null, never an exception.</summary>
    public static TimeSpan? ParseDuration(string text)
    {
        var total = TimeSpan.Zero;
        var any = false;
        foreach (Match m in DurationPart.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                continue;
            any = true;
            total += m.Groups[2].Value.ToLowerInvariant() switch
            {
                "h" => TimeSpan.FromHours(n),
                "min" => TimeSpan.FromMinutes(n),
                _ => TimeSpan.FromSeconds(n),
            };
        }

        return any ? total : null;
    }

    /// <summary>Accepts only site-relative paths ("/new/2", "/?k=x&amp;p=1"); anything else is dropped.</summary>
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\'))
            return null;
        return path;
    }

    private static bool IsEmptyResult(string html) =>
        html.Contains("No video match with this search", StringComparison.OrdinalIgnoreCase)
        || (html.Contains("class=\"mozaique", StringComparison.OrdinalIgnoreCase)
            && !html.Contains("href=\"/video.", StringComparison.OrdinalIgnoreCase));

    private static Uri? AbsUrl(Match m) =>
        m.Success && Uri.TryCreate(WebUtility.HtmlDecode(m.Groups[1].Value), UriKind.Absolute, out var u) ? u : null;

    private static string Decode(string s) => WebUtility.HtmlDecode(s).Trim();
}
