using System.Globalization;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.XHamster;

/// <summary>
/// xHamster source. Reads the public pages' embedded JSON state and hands Emby the plain
/// HLS URL each video page preloads. It never decodes the player's obfuscated stream
/// URLs, never uses robots.txt-disallowed paths (/embed/, duration filters, search pages
/// beyond 10) and never works around geo or age restrictions: geo-blocked items are
/// skipped and a missing plain stream fails with <see cref="ProviderErrorKind.Unsupported"/>.
///
/// Site facts come from the live site (2026-10-11) and, as a functional reference only,
/// Cumination's xhamster module (GPL; no code copied, see ADR-003).
/// </summary>
public sealed class XHamsterProvider : ITubeProvider
{
    public const string ProviderId = "xhamster";
    public const string BaseUrl = "https://xhamster.com/";

    /// <summary>robots.txt allows search result pages 2–10 only.</summary>
    public const int MaxSearchPage = 10;

    private static readonly Uri Base = new(BaseUrl);
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);
    private static readonly Regex CategoryId = new(@"^[a-z0-9\-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex VideoIdPattern = new(@"^[A-Za-z0-9_\-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex SearchPage = new(@"[?&]page=(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly SafeHttpClient _http;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    public XHamsterProvider(SafeHttpClient http, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public string Id => ProviderId;

    public string DisplayName => "xHamster";

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Popular, TubeSorts.Newest },
        Formats = new[] { StreamProtocol.Hls },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; } = new[] { "xhamster.com", "xhcdn.com" };

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var items = XHamsterParser.ParseCategories(await GetAsync("/", ct).ConfigureAwait(false));
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
            if (!CategoryId.IsMatch(request.CategoryId))
                throw new ProviderException(ProviderErrorKind.NotFound, "Unknown xHamster category.");
            path = "/categories/" + request.CategoryId;
        }
        else
        {
            path = request.Sort == TubeSorts.Newest ? "/newest" : "/best";
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
            var page = XHamsterParser.ParseVideoPage(await GetAsync("/videos/" + videoId, ct).ConfigureAwait(false), Id, videoId);
            return page.Video;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!VideoIdPattern.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid xHamster video id.");

        var page = XHamsterParser.ParseVideoPage(await GetAsync("/videos/" + videoId, ct).ConfigureAwait(false), Id, videoId);
        if (page.Deleted)
            throw new ProviderException(ProviderErrorKind.NotFound, "xHamster video was deleted.");
        if (page.Hls is null)
            throw new ProviderException(ProviderErrorKind.Unsupported, "xHamster offers no plain stream for this video.");

        int? height = null;
        try
        {
            height = XHamsterParser.MaxHeight(await _http.GetStringAsync(page.Hls, AllowedHosts, ct).ConfigureAwait(false));
        }
        catch (ProviderException)
        {
            // Height is only a hint.
        }

        return new PlaybackResult(new[] { new PlaybackVariant(page.Hls, StreamProtocol.Hls, "hls") { Height = height } })
        {
            Duration = page.Video?.Duration,
        };
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        var listing = XHamsterParser.ParseListing(await GetAsync(path, ct).ConfigureAwait(false), Id);
        var next = listing.NextPath;
        if (next is not null && next.StartsWith("/search/", StringComparison.Ordinal)
            && SearchPage.Match(next) is { Success: true } m
            && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) > MaxSearchPage)
            next = null;
        return new Page<TubeVideo>(listing.Videos, next == path ? null : next);
    }

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(Base, path), AllowedHosts, ct);

    private static string ValidateToken(string token) =>
        XHamsterParser.NormalizePath(token) ?? throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid xHamster page token.");
}
