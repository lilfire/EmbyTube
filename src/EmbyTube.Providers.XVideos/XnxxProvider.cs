using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.XVideos;

/// <summary>
/// XNXX source. Same platform as XVideos (shared <see cref="XVideosParser"/> with the
/// <see cref="XvSite.Xnxx"/> profile): public HTML pages, the page's own
/// <c>html5player.setVideoHLS/UrlHigh/UrlLow</c> URLs resolved at playback time, honest
/// User-Agent, no cookies. XNXX has no "newest" listing; it offers "best of month" and an
/// editorial "today's selection". Categories are the site's own category list, most of
/// which are curated searches.
///
/// Site facts from the live site (2026-10-11) and, as a functional reference only,
/// Cumination's xnxx module (GPL; no code copied, see ADR-003).
/// </summary>
public sealed class XnxxProvider : ITubeProvider
{
    public const string ProviderId = "xnxx";
    public const string BaseUrl = "https://www.xnxx.com/";

    private static readonly Uri Base = new(BaseUrl);
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);
    private static readonly Regex CategoryJson = new(@"write_thumb_block_list\((\[.*?\])\s*[,)]", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
    private static readonly Regex VideoIdPattern = new(@"^[a-z0-9]+(?:/[A-Za-z0-9_\-\.]+)+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex BestPath = new(@"^/best/(\d{4}-\d{2})(?:/(\d+))?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly SafeHttpClient _http;
    private readonly bool _sfwThumbnails;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    public XnxxProvider(SafeHttpClient http, bool sfwThumbnails = false, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _sfwThumbnails = sfwThumbnails;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public string Id => ProviderId;

    public string DisplayName => "XNXX";

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Popular, TubeSorts.Featured },
        Formats = new[] { StreamProtocol.Hls, StreamProtocol.Progressive },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; } = new[] { "xnxx.com", "xnxx-cdn.com" };

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var items = ParseCategories(await GetAsync("/", ct).ConfigureAwait(false));
            _categories = (items, _now() + CategoryTtl);
            return items;
        }
        finally
        {
            _categoryLock.Release();
        }
    }

    /// <summary>Categories are embedded as JSON (<c>cats.write_thumb_block_list([...])</c>); ids are site paths.</summary>
    public static IReadOnlyList<TubeCategory> ParseCategories(string html)
    {
        var m = CategoryJson.Match(html);
        if (!m.Success)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "XNXX categories not found.");

        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(m.Groups[1].Value);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("u", out var u) || u.ValueKind != JsonValueKind.String
                    || !item.TryGetProperty("tf", out var tf) || tf.ValueKind != JsonValueKind.String)
                    continue;
                var path = XVideosParser.NormalizePath(u.GetString());
                var name = WebUtility.HtmlDecode(tf.GetString() ?? string.Empty).Trim();
                if (path is null || !IsListingPath(path) || name.Length == 0 || !seen.Add(path))
                    continue;
                cats.Add(new TubeCategory(path, name));
            }
        }
        catch (JsonException ex)
        {
            throw new ProviderException(ProviderErrorKind.ParseChanged, "XNXX categories are not valid JSON.", ex);
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, "XNXX categories not found.");
        return cats;
    }

    public Task<Page<TubeVideo>> BrowseAsync(BrowseRequest request, CancellationToken ct)
    {
        string path;
        if (request.PageToken is not null)
        {
            path = ValidateToken(request.PageToken);
        }
        else if (request.CategoryId is not null)
        {
            path = XVideosParser.NormalizePath(request.CategoryId) is { } p && IsListingPath(p)
                ? p
                : throw new ProviderException(ProviderErrorKind.NotFound, "Unknown XNXX category.");
        }
        else
        {
            path = request.Sort == TubeSorts.Featured ? "/todays-selection" : "/best/" + PreviousMonth();
        }

        return ListAsync(path, ct);
    }

    public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var path = request.PageToken is not null
            ? ValidateToken(request.PageToken)
            : "/search/" + Uri.EscapeDataString(request.Query.Trim());
        return ListAsync(path, ct);
    }

    public async Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            return null;
        try
        {
            var html = await GetAsync("/video-" + videoId, ct).ConfigureAwait(false);
            return XVideosParser.ParseVideoPage(html, Id, videoId, XvSite.Xnxx).Video;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid XNXX video id.");

        var html = await GetAsync("/video-" + videoId, ct).ConfigureAwait(false);
        var page = XVideosParser.ParseVideoPage(html, Id, videoId, XvSite.Xnxx);
        if (page.Hls is null && page.Mp4High is null && page.Mp4Low is null)
            throw new ProviderException(ProviderErrorKind.AccessDenied, "XNXX did not provide a stream for this video.");

        var variants = new List<PlaybackVariant>(3);
        if (page.Hls is not null)
        {
            int? height = null;
            try
            {
                height = XVideosParser.MaxHeight(await _http.GetStringAsync(page.Hls, AllowedHosts, ct).ConfigureAwait(false));
            }
            catch (ProviderException)
            {
                // Height is only a hint.
            }

            variants.Add(new PlaybackVariant(page.Hls, StreamProtocol.Hls, "hls") { Height = height });
        }

        if (page.Mp4High is not null)
            variants.Add(new PlaybackVariant(page.Mp4High, StreamProtocol.Progressive, "mp4"));
        if (page.Mp4Low is not null)
            variants.Add(new PlaybackVariant(page.Mp4Low, StreamProtocol.Progressive, "mp4"));

        return new PlaybackResult(variants) { Duration = page.Video.Duration };
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        var html = await GetAsync(path, ct).ConfigureAwait(false);
        var listing = XVideosParser.ParseListing(html, Id, _sfwThumbnails, XvSite.Xnxx);
        var next = listing.NextPath;

        // Today's selection has no pagination block; its pages are /todays-selection/{n}.
        if (next is null && listing.Videos.Count > 0 && path.StartsWith("/todays-selection", StringComparison.Ordinal))
        {
            var n = path.Length > "/todays-selection/".Length
                && int.TryParse(path["/todays-selection/".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var cur)
                    ? cur + 1
                    : 1;
            next = string.Create(CultureInfo.InvariantCulture, $"/todays-selection/{n}");
        }
        else if (next is null && listing.Videos.Count > 0 && BestPath.Match(path) is { Success: true } best)
        {
            var n = best.Groups[2].Success ? int.Parse(best.Groups[2].Value, CultureInfo.InvariantCulture) + 1 : 1;
            next = string.Create(CultureInfo.InvariantCulture, $"/best/{best.Groups[1].Value}/{n}");
        }

        return new Page<TubeVideo>(listing.Videos, next == path ? null : next);
    }

    /// <summary>Only listing-type paths may be used as category ids or page tokens.</summary>
    private static bool IsListingPath(string path) =>
        path.StartsWith("/search/", StringComparison.Ordinal)
        || path.StartsWith("/todays-selection", StringComparison.Ordinal)
        || path.StartsWith("/best/", StringComparison.Ordinal)
        || path.StartsWith("/hits", StringComparison.Ordinal);

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(Base, path), AllowedHosts, ct);

    private static string ValidateToken(string token) =>
        XVideosParser.NormalizePath(token) is { } p && IsListingPath(p)
            ? p
            : throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid XNXX page token.");

    private string PreviousMonth()
    {
        var now = _now().UtcDateTime;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}
