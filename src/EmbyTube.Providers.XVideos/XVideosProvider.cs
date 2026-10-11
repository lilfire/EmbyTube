using System.Globalization;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.XVideos;

public sealed record XVideosOptions
{
    /// <summary>Use the site's "SFW" thumbnail variant instead of the regular one.</summary>
    public bool SfwThumbnails { get; init; }
}

/// <summary>
/// XVideos source. Reads the public HTML pages a browser would load (no login, no API key)
/// and hands Emby the site's own HLS/MP4 URLs at playback time. It does not bypass age
/// verification, geo-blocking or any other access control: if the site withholds streams
/// (for example behind an age-verification gate in some regions) resolve fails with
/// <see cref="ProviderErrorKind.AccessDenied"/>.
///
/// Site facts were taken from the live site's markup (2026-10-11) and, as a functional
/// reference only, from Cumination's xvideos module (GPL; no code copied, see ADR-003).
/// </summary>
public sealed class XVideosProvider : ITubeProvider
{
    public const string ProviderId = "xvideos";
    public const string BaseUrl = "https://www.xvideos.com/";

    private static readonly Uri Base = new(BaseUrl);
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);
    private static readonly Regex BestPath = new(@"^/best/(\d{4}-\d{2})(?:/(\d+))?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex VideoIdPattern = new(@"^[a-z0-9]+/[A-Za-z0-9_\-\.%]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly SafeHttpClient _http;
    private readonly XVideosOptions _options;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    public XVideosProvider(SafeHttpClient http, XVideosOptions? options = null, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _options = options ?? new XVideosOptions();
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public string Id => ProviderId;

    public string DisplayName => "XVideos";

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Popular, TubeSorts.Newest },
        Formats = new[] { StreamProtocol.Hls, StreamProtocol.Progressive },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; } = new[] { "xvideos.com", "xvideos-cdn.com" };

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var html = await GetAsync("/", ct).ConfigureAwait(false);
            var items = XVideosParser.ParseCategories(html);
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
        {
            path = ValidateToken(request.PageToken);
        }
        else if (request.CategoryId is not null)
        {
            if (!Regex.IsMatch(request.CategoryId, @"^[A-Za-z0-9_]+-\d+$", RegexOptions.None, TimeSpan.FromSeconds(1)))
                throw new ProviderException(ProviderErrorKind.NotFound, "Unknown XVideos category.");
            path = "/c/" + request.CategoryId;
        }
        else if (request.Sort == TubeSorts.Newest)
        {
            path = "/new/1";
        }
        else
        {
            path = "/best/" + PreviousMonth();
        }

        return ListAsync(path, ct);
    }

    public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var path = request.PageToken is not null
            ? ValidateToken(request.PageToken)
            : "/?k=" + Uri.EscapeDataString(request.Query.Trim());
        return ListAsync(path, ct);
    }

    public async Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            return null;
        try
        {
            var html = await GetAsync("/video." + videoId, ct).ConfigureAwait(false);
            return XVideosParser.ParseVideoPage(html, Id, videoId).Video;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid XVideos video id.");

        var html = await GetAsync("/video." + videoId, ct).ConfigureAwait(false);
        var page = XVideosParser.ParseVideoPage(html, Id, videoId);

        if (page.Hls is null && page.Mp4High is null && page.Mp4Low is null)
        {
            // Page parsed but streams are withheld (e.g. age verification required in
            // this region). Respect it; never try to work around it.
            throw new ProviderException(ProviderErrorKind.AccessDenied, "XVideos did not provide a stream for this video.");
        }

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
                // Height is only a hint; the HLS variant stays usable.
            }

            variants.Add(new PlaybackVariant(page.Hls, StreamProtocol.Hls, "hls") { Height = height });
        }

        // Progressive files are SD fallbacks of unstated height (Emby's probe reports it);
        // "high" before "low" (PlaybackService's ordering is stable for ties). Heights are
        // left unknown rather than guessed.
        if (page.Mp4High is not null)
            variants.Add(new PlaybackVariant(page.Mp4High, StreamProtocol.Progressive, "mp4"));
        if (page.Mp4Low is not null)
            variants.Add(new PlaybackVariant(page.Mp4Low, StreamProtocol.Progressive, "mp4"));

        return new PlaybackResult(variants) { Duration = page.Video.Duration };
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        var html = await GetAsync(path, ct).ConfigureAwait(false);
        var listing = XVideosParser.ParseListing(html, Id, _options.SfwThumbnails);

        var next = listing.NextPath;
        if (BestPath.Match(path) is { Success: true } best && listing.Videos.Count > 0)
        {
            // "Best of month" pages have numbered links but no "next" marker.
            var n = best.Groups[2].Success ? int.Parse(best.Groups[2].Value, CultureInfo.InvariantCulture) + 1 : 1;
            next = string.Create(CultureInfo.InvariantCulture, $"/best/{best.Groups[1].Value}/{n}");
        }

        return new Page<TubeVideo>(listing.Videos, next == path ? null : next);
    }

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(Base, path), AllowedHosts, ct);

    private static string ValidateToken(string token) =>
        XVideosParser.NormalizePath(token) ?? throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid XVideos page token.");

    private string PreviousMonth()
    {
        var now = _now().UtcDateTime;
        var prev = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        return prev.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}
