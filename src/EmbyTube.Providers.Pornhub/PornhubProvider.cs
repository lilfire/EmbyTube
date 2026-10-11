using System.Globalization;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.Pornhub;

/// <summary>
/// Pornhub source. Reads public pages without any cookies — in particular it never sends
/// the age-disclaimer/UK age-verification acceptance cookies that some scrapers pre-set.
/// If the site withholds streams (age verification, geo block), resolve fails with
/// <see cref="ProviderErrorKind.AccessDenied"/>.
///
/// The CDN only serves HLS with a <c>Referer</c> from pornhub.com, so variants carry
/// <c>Referer</c> plus EmbyTube's own <c>User-Agent</c> as required headers (spec §6.4);
/// clients cannot send them, so Emby fetches the stream server-side.
/// robots.txt is respected: no sort/filter parameters on search, search pages ≤ 9, no
/// cc=/hd=/duration filters.
///
/// Site facts from the live site (2026-10-11) and, as a functional reference only,
/// Cumination's pornhub module (GPL; no code copied, see ADR-003).
/// </summary>
public sealed class PornhubProvider : ITubeProvider
{
    public const string ProviderId = "pornhub";
    public const string BaseUrl = "https://www.pornhub.com/";

    /// <summary>robots.txt allows plain search result pages 2–9 only.</summary>
    public const int MaxSearchPage = 9;

    private static readonly Uri Base = new(BaseUrl);
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);
    private static readonly Regex Slug = new(@"^[a-z0-9\-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex NumericCategory = new(@"^c(\d+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex ViewKey = new(@"^[a-z0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex PageParam = new(@"[?&]page=(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex AllowedListing = new(@"^/(?:video\?o=(?:cm|mv)(?:&page=\d+)?|video\?c=\d+(?:&page=\d+)?|categories/[a-z0-9\-]+(?:\?page=\d+)?|video/search\?search=[^&]+(?:&page=\d+)?)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly SafeHttpClient _http;
    private readonly IReadOnlyDictionary<string, string> _streamHeaders;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    public PornhubProvider(SafeHttpClient http, string userAgent, Func<DateTimeOffset>? now = null)
    {
        _http = http;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _streamHeaders = new Dictionary<string, string>
        {
            ["Referer"] = BaseUrl,
            ["User-Agent"] = userAgent,
        };
    }

    public string Id => ProviderId;

    public string DisplayName => "Pornhub";

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Popular, TubeSorts.Newest },
        Formats = new[] { StreamProtocol.Hls },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; } = new[] { "pornhub.com", "phncdn.com" };

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var items = PornhubParser.ParseCategories(await GetAsync("/categories", ct).ConfigureAwait(false));
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
            path = NumericCategory.Match(request.CategoryId) is { Success: true } c ? "/video?c=" + c.Groups[1].Value
                : Slug.IsMatch(request.CategoryId) ? "/categories/" + request.CategoryId
                : throw new ProviderException(ProviderErrorKind.NotFound, "Unknown Pornhub category.");
        else
            path = request.Sort == TubeSorts.Newest ? "/video?o=cm" : "/video?o=mv";

        return ListAsync(path, ct);
    }

    public async Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var path = request.PageToken is not null
            ? ValidateToken(request.PageToken)
            : "/video/search?search=" + Uri.EscapeDataString(request.Query.Trim()).Replace("%20", "+", StringComparison.Ordinal);
        try
        {
            return await ListAsync(path, ct).ConfigureAwait(false);
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound && request.PageToken is null)
        {
            // Pornhub answers a search without results with HTTP 404.
            return Page<TubeVideo>.Empty;
        }
    }

    public async Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        if (!ViewKey.IsMatch(videoId))
            return null;
        try
        {
            return PornhubParser.ParseVideoPage(await GetAsync("/view_video.php?viewkey=" + videoId, ct).ConfigureAwait(false), Id, videoId).Video;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!ViewKey.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid Pornhub video id.");

        var page = PornhubParser.ParseVideoPage(await GetAsync("/view_video.php?viewkey=" + videoId, ct).ConfigureAwait(false), Id, videoId);
        if (page.Hls.Count == 0)
            throw new ProviderException(ProviderErrorKind.AccessDenied, "Pornhub did not provide a stream for this video.");

        var variants = page.Hls
            .Select(s => new PlaybackVariant(s.Url, StreamProtocol.Hls, "hls") { Height = s.Height, Headers = _streamHeaders })
            .ToList();
        return new PlaybackResult(variants) { Duration = page.Video?.Duration };
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        var listing = PornhubParser.ParseListing(await GetAsync(path, ct).ConfigureAwait(false), Id);
        var next = listing.NextPath is { } n && AllowedListing.IsMatch(n) ? n : null;
        if (next is not null && next.StartsWith("/video/search", StringComparison.Ordinal)
            && PageParam.Match(next) is { Success: true } m
            && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) > MaxSearchPage)
            next = null;
        return new Page<TubeVideo>(listing.Videos, next == path ? null : next);
    }

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(Base, path), AllowedHosts, ct);

    private static string ValidateToken(string token) =>
        PornhubParser.NormalizePath(token) is { } p && AllowedListing.IsMatch(p)
            ? p
            : throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid Pornhub page token.");
}
