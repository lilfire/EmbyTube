using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.Pornhat;

/// <summary>One site of the Pornhat network (shared platform and markup).</summary>
public sealed record PornhatSite(string Id, string DisplayName, string BaseUrl)
{
    public static readonly PornhatSite Pornhat = new("pornhat", "Pornhat", "https://www.pornhat.com/");
    public static readonly PornhatSite OkXxx = new("okxxx", "OK XXX", "https://ok.xxx/");
    public static readonly PornhatSite PerfectGirls = new("perfectgirls", "Perfect Girls", "https://www.perfectgirls.xxx/");
    public static readonly PornhatSite OkPorn = new("okporn", "OK Porn", "https://ok.porn/");

    public static IReadOnlyList<PornhatSite> All { get; } = new[] { Pornhat, OkXxx, PerfectGirls, OkPorn };

    public string Host => new Uri(BaseUrl).Host;
}

/// <summary>
/// Pornhat-network source (pornhat.com, ok.xxx, perfectgirls.xxx, ok.porn). Lists come from
/// the public pages; channels (studios) serve as categories. Playback uses the page's plain
/// <c>&lt;source src label="720p"&gt;</c> URLs, which redirect to HLS on cdn.privatehost.com;
/// the redirect is resolved here so Emby gets the final, allowlist-checked CDN URL. No
/// Referer or cookies are needed. robots.txt disallows only async/action/player endpoints,
/// none of which are used.
///
/// Site facts from the live sites (2026-10-11) and, as a functional reference only,
/// Cumination's pornhat module (GPL; no code copied, see ADR-003).
/// </summary>
public sealed class PornhatNetworkProvider : ITubeProvider
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);

    private static readonly Regex VideoAnchor = new(@"<a\s+href=""(?:https?://[^/""]+)?/video/([A-Za-z0-9\-]+)/""\s+title=""([^""]+)""", Opts, RegexTimeout);
    private static readonly Regex Thumb = new(@"\bdata-(?:original|src)=""(https://[^""]+)""", Opts, RegexTimeout);
    private static readonly Regex Duration = new(@"(?:duration_item""\s*>|fa-clock-o""></i>\s*<span>)\s*([\d:]+)\s*<", Opts, RegexTimeout);
    private static readonly Regex NextLink = new(@"href=""([^""]+)""[^>]*>\s*Next\b", Opts, RegexTimeout);
    // Channel tiles: <a title="Name" href="/sites/slug/"> (most sites) or /channels/slug/ (perfectgirls).
    private static readonly Regex Channel = new(@"<a\s+title=""([^""]+)""\s+href=""(?:https?://[^/""]+)?/((?:sites|channels)/[A-Za-z0-9\-]+)/""", Opts, RegexTimeout);
    private static readonly Regex ChannelId = new(@"^(?:sites|channels)/[A-Za-z0-9\-]+$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex Source = new(@"<source\s+src=""(https://[^""]+)""[^>]*?\b(?:label|title)=""(\d{3,4})p""", Opts | RegexOptions.Singleline, RegexTimeout);
    private static readonly Regex OgTitle = new(@"<meta property=""og:title"" content=""([^""]*)""", Opts, RegexTimeout);
    private static readonly Regex Slug = new(@"^[A-Za-z0-9\-]+$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex AllowedListing = new(@"^/(?:\d+/|(?:sites|channels)/[A-Za-z0-9\-]+/(?:\d+/)?|search/[^/?#]+/(?:\d+/)?)?$", RegexOptions.CultureInvariant, RegexTimeout);

    private readonly PornhatSite _site;
    private readonly SafeHttpClient _http;
    private readonly Uri _base;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    public PornhatNetworkProvider(PornhatSite site, SafeHttpClient http, Func<DateTimeOffset>? now = null)
    {
        _site = site;
        _http = http;
        _base = new Uri(site.BaseUrl);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        AllowedHosts = new[] { site.Host.StartsWith("www.", StringComparison.Ordinal) ? site.Host[4..] : site.Host, "privatehost.com" };
    }

    public string Id => _site.Id;

    public string DisplayName => _site.DisplayName;

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Newest },
        Formats = new[] { StreamProtocol.Hls },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; }

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var items = ParseChannels(await GetAsync("/channels/", ct).ConfigureAwait(false));
            _categories = (items, _now() + CategoryTtl);
            return items;
        }
        finally
        {
            _categoryLock.Release();
        }
    }

    public Task<Page<TubeVideo>> BrowseAsync(BrowseRequest request, CancellationToken ct)
    {
        string path;
        if (request.PageToken is not null)
            path = ValidateToken(request.PageToken);
        else if (request.CategoryId is not null)
            path = ChannelId.IsMatch(request.CategoryId) ? "/" + request.CategoryId + "/" : throw new ProviderException(ProviderErrorKind.NotFound, "Unknown channel.");
        else
            path = "/";
        return ListAsync(path, ct);
    }

    public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        if (request.PageToken is not null)
            return ListAsync(ValidateToken(request.PageToken), ct);

        // The sites expect words joined by '-': /search/two-words/
        var words = Regex.Split(request.Query.Trim(), @"\s+", RegexOptions.None, RegexTimeout).Where(w => w.Length > 0);
        return ListAsync("/search/" + Uri.EscapeDataString(string.Join('-', words)) + "/", ct);
    }

    public async Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        if (!Slug.IsMatch(videoId))
            return null;
        try
        {
            var html = await GetAsync("/video/" + videoId + "/", ct).ConfigureAwait(false);
            return OgTitle.Match(html) is { Success: true } t
                ? new TubeVideo(Id, videoId, WebUtility.HtmlDecode(t.Groups[1].Value).Trim()) { SourcePageUrl = _site.BaseUrl + "video/" + videoId + "/" }
                : null;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!Slug.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid video id.");

        var html = await GetAsync("/video/" + videoId + "/", ct).ConfigureAwait(false);
        var sources = Source.Matches(html)
            .Select(m => (Url: WebUtility.HtmlDecode(m.Groups[1].Value), Height: int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .GroupBy(s => s.Height)
            .Select(g => g.First())
            .OrderByDescending(s => s.Height)
            .ToList();
        if (sources.Count == 0)
            throw new ProviderException(ProviderErrorKind.AccessDenied, $"{DisplayName} did not provide a stream for this video.");

        var variants = new List<PlaybackVariant>(sources.Count);
        foreach (var (url, height) in sources)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                continue;
            try
            {
                var final = await _http.ResolveRedirectsAsync(uri, AllowedHosts, ct).ConfigureAwait(false);
                variants.Add(new PlaybackVariant(final, StreamProtocol.Hls, "hls") { Height = height });
            }
            catch (ProviderException ex) when (ex.Kind is ProviderErrorKind.Blocked or ProviderErrorKind.NotFound)
            {
                // A single broken or off-allowlist quality must not hide the others.
            }
        }

        if (variants.Count == 0)
            throw new ProviderException(ProviderErrorKind.Unsupported, $"{DisplayName}: no usable stream.");
        return new PlaybackResult(variants);
    }

    internal IReadOnlyList<TubeVideo> ParseListing(string html, out string? next)
    {
        var anchors = VideoAnchor.Matches(html);
        var videos = new List<TubeVideo>(anchors.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < anchors.Count; i++)
        {
            var id = anchors[i].Groups[1].Value;
            if (!seen.Add(id))
                continue; // the title link repeats the thumbnail link
            var end = Math.Min(html.Length, anchors[i].Index + 2_500);
            for (var j = i + 1; j < anchors.Count; j++)
            {
                if (anchors[j].Groups[1].Value != id)
                {
                    end = anchors[j].Index;
                    break;
                }
            }

            var block = html[anchors[i].Index..end];
            videos.Add(new TubeVideo(Id, id, WebUtility.HtmlDecode(anchors[i].Groups[2].Value).Trim())
            {
                ThumbnailUrl = Thumb.Match(block) is { Success: true } th ? WebUtility.HtmlDecode(th.Groups[1].Value) : null,
                Duration = Duration.Match(block) is { Success: true } d ? ParseClock(d.Groups[1].Value) : null,
                SourcePageUrl = _site.BaseUrl + "video/" + id + "/",
            });
        }

        if (videos.Count == 0 && !html.Contains("<div class=\"main", StringComparison.Ordinal) && !html.Contains("list_video", StringComparison.Ordinal))
            throw new ProviderException(ProviderErrorKind.ParseChanged, $"{DisplayName} listing layout not recognized.");

        next = NextLink.Match(html) is { Success: true } n ? NormalizePath(WebUtility.HtmlDecode(n.Groups[1].Value)) : null;
        return videos;
    }

    internal static IReadOnlyList<TubeCategory> ParseChannels(string html)
    {
        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Channel.Matches(html))
        {
            if (seen.Add(m.Groups[2].Value))
                cats.Add(new TubeCategory(m.Groups[2].Value, WebUtility.HtmlDecode(m.Groups[1].Value).Trim()));
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "Channel list not found.");
        return cats;
    }

    internal static TimeSpan? ParseClock(string text)
    {
        var total = 0;
        var parts = text.Split(':');
        if (parts.Length is < 2 or > 3)
            return null;
        foreach (var p in parts)
        {
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                return null;
            total = (total * 60) + n;
        }

        return TimeSpan.FromSeconds(total);
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        string html;
        try
        {
            html = await GetAsync(path, ct).ConfigureAwait(false);
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound && path.StartsWith("/search/", StringComparison.Ordinal))
        {
            return Page<TubeVideo>.Empty;
        }

        var videos = ParseListing(html, out var next);
        return new Page<TubeVideo>(videos, next is not null && next != path && AllowedListing.IsMatch(next) ? next : null);
    }

    private string? NormalizePath(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (value.StartsWith(_site.BaseUrl, StringComparison.Ordinal))
            value = "/" + value[_site.BaseUrl.Length..];
        return value[0] == '/' && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') ? value : null;
    }

    private string ValidateToken(string token) =>
        NormalizePath(token) is { } p && AllowedListing.IsMatch(p)
            ? p
            : throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid page token.");

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(_base, path), AllowedHosts, ct);
}
