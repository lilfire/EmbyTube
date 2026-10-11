using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Providers.Html;

/// <summary>
/// Generic provider for classic HTML tube sites described by an <see cref="HtmlTubeSite"/>.
/// Listings, categories and search are parsed from public pages; streams are taken only from
/// plain URLs the site itself exposes (see <see cref="StreamStrategy"/>), with redirects
/// resolved and allowlist-checked. No cookies, no Referer, honest User-Agent.
/// </summary>
public sealed class HtmlTubeProvider : ITubeProvider
{
    private static readonly TimeSpan CategoryTtl = TimeSpan.FromHours(6);
    private static readonly Regex SourceTag = new(@"<source\b[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    private static readonly Regex Attr = new(@"\b(src|label|title|size|res|type)\s*=\s*""([^""]*)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    private static readonly Regex Digits = new(@"(\d{3,4})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex EmbedUrl = new(@"""embedUrl""\s*:\s*""([^""]+)""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex DrTuberVid = new(@"^video/(\d+)/", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex KvsUrl = new(@"\b(video_(?:alt_)?url\d*)\s*:\s*'([^']*)'", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex ContentUrl = new(@"(?:""contentUrl""\s*:\s*""|itemprop=""contentURL""\s+content="")(https?:[^""]+)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    private static readonly Regex Mp4Url = new(@"[""'](https://[^""'\s]+\.mp4[^""'\s]*)[""']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    private static readonly Regex Minutes = new(@"^\s*(\d+)\s*m(?:in)?\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex MinSec = new(@"^\s*(?:(\d+)h:?)?(\d+)m:?(\d+)s\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex OgTitle = new(@"<meta property=""og:title"" content=""([^""]*)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

    private readonly HtmlTubeSite _site;
    private readonly SafeHttpClient _http;
    private readonly Uri _base;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _categoryLock = new(1, 1);
    private (IReadOnlyList<TubeCategory> Items, DateTimeOffset Expires)? _categories;

    private readonly IReadOnlyDictionary<string, string> _streamHeaders;

    public HtmlTubeProvider(HtmlTubeSite site, SafeHttpClient http, Func<DateTimeOffset>? now = null, string userAgent = "EmbyTube")
    {
        _site = site;
        _streamHeaders = site.StreamNeedsReferer
            ? new Dictionary<string, string> { ["Referer"] = site.BaseUrl, ["User-Agent"] = userAgent }
            : new Dictionary<string, string>();
        _http = http;
        _base = new Uri(site.BaseUrl);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        Capabilities = new ProviderCapabilities
        {
            Browse = true,
            Categories = site.CategoriesPage is not null && site.CategoryLink is not null && site.CategoryPath is not null,
            Search = site.SearchTemplate is not null,
            Pagination = true,
            Sorts = site.Sorts.Keys.ToArray(),
            Formats = new[] { StreamProtocol.Progressive, StreamProtocol.Hls },
        };
    }

    public string Id => _site.Id;

    public string DisplayName => _site.DisplayName;

    public ProviderCapabilities Capabilities { get; }

    public IReadOnlyCollection<string> AllowedHosts => _site.AllowedHosts;

    public async Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        if (!Capabilities.Categories)
            return Array.Empty<TubeCategory>();
        if (_categories is { } c && c.Expires > _now())
            return c.Items;

        await _categoryLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_categories is { } c2 && c2.Expires > _now())
                return c2.Items;
            var items = ParseCategories(await GetAsync(_site.CategoriesPage!, ct).ConfigureAwait(false));
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
            path = ValidateListing(request.PageToken);
        }
        else if (request.CategoryId is not null)
        {
            path = _site.CategoryPath is { } cp && cp.IsMatch(request.CategoryId)
                ? "/" + request.CategoryId
                : throw new ProviderException(ProviderErrorKind.NotFound, "Unknown category.");
        }
        else
        {
            path = request.Sort is not null && _site.Sorts.TryGetValue(request.Sort, out var p) ? p : _site.Sorts.First().Value;
        }

        return ListAsync(path, ct);
    }

    public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        if (_site.SearchTemplate is null)
            throw new ProviderException(ProviderErrorKind.Unsupported, "Search is not available for this source.");
        if (request.PageToken is not null)
            return ListAsync(ValidateListing(request.PageToken), ct);

        var words = Regex.Split(request.Query.Trim(), @"\s+", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Where(w => w.Length > 0)
            .Select(Uri.EscapeDataString);
        return ListAsync(_site.SearchTemplate.Replace("{q}", string.Join(_site.SearchSpace, words), StringComparison.Ordinal), ct);
    }

    public async Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        if (!_site.VideoPath.IsMatch(videoId))
            return null;
        try
        {
            var html = await GetAsync("/" + videoId, ct).ConfigureAwait(false);
            return OgTitle.Match(html) is { Success: true } t
                ? new TubeVideo(Id, videoId, WebUtility.HtmlDecode(t.Groups[1].Value).Trim()) { SourcePageUrl = _site.BaseUrl + videoId }
                : null;
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound)
        {
            return null;
        }
    }

    public async Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        if (!_site.VideoPath.IsMatch(videoId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Invalid video id.");

        var candidates = _site.Streams switch
        {
            StreamStrategy.DrTuberJson => await DrTuberStreamsAsync(videoId, ct).ConfigureAwait(false),
            StreamStrategy.TnaflixAjax => await TnaflixStreamsAsync(videoId, ct).ConfigureAwait(false),
            StreamStrategy.KvsFlashvars => ParseKvs(await GetAsync("/" + videoId, ct).ConfigureAwait(false)),
            StreamStrategy.SubPage => await SubPageStreamsAsync(videoId, ct).ConfigureAwait(false),
            _ => ParseSourceTags(await GetAsync("/" + videoId, ct).ConfigureAwait(false), includeContentUrl: true, new Uri(_base, "/" + videoId)),
        };
        if (_site.StreamFilter is { } filter)
            candidates = candidates.Where(c => filter.IsMatch(c.Url.AbsoluteUri)).ToList();
        if (candidates.Count == 0)
            throw new ProviderException(ProviderErrorKind.AccessDenied, $"{DisplayName} did not provide a stream for this video.");

        var variants = new List<PlaybackVariant>();
        foreach (var (url, height) in candidates.GroupBy(c => c.Url).Select(g => g.First()).OrderByDescending(c => c.Height ?? 0))
        {
            try
            {
                var resolved = await _http.ResolveStreamAsync(url, AllowedHosts, ct, _streamHeaders.Count > 0 ? _streamHeaders : null).ConfigureAwait(false);
                var hls = resolved.IsHls;
                variants.Add(new PlaybackVariant(resolved.Url, hls ? StreamProtocol.Hls : StreamProtocol.Progressive, hls ? "hls" : "mp4")
                {
                    Height = height,
                    Headers = _streamHeaders,
                });
            }
            catch (ProviderException ex) when (ex.Kind is ProviderErrorKind.Blocked or ProviderErrorKind.NotFound or ProviderErrorKind.AccessDenied)
            {
                // One broken quality must not hide the others.
            }

            if (variants.Count >= 4)
                break;
        }

        if (variants.Count == 0)
            throw new ProviderException(ProviderErrorKind.Unsupported, $"{DisplayName}: no usable stream.");
        return new PlaybackResult(variants);
    }

    internal IReadOnlyList<TubeVideo> ParseListing(string html, out string? next, string? currentPath = null)
    {
        var starts = _site.ItemStart.Matches(html);
        var videos = new List<TubeVideo>(starts.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].Index : Math.Min(html.Length, starts[i].Index + 4_000);
            var block = html[starts[i].Index..end];
            if (_site.SkipItem?.IsMatch(block) == true)
                continue;
            if (_site.ItemLink.Match(block) is not { Success: true } link || ToSitePath(WebUtility.HtmlDecode(First(link))) is not { } path)
                continue;
            var id = path.TrimStart('/');
            var hash = id.IndexOf('#');
            if (hash >= 0)
                id = id[..hash];
            if (!_site.VideoPath.IsMatch(id) || !seen.Add(id))
                continue;
            if (_site.ItemTitle.Match(block) is not { Success: true } title)
                continue;

            videos.Add(new TubeVideo(Id, id, WebUtility.HtmlDecode(First(title)).Trim())
            {
                ThumbnailUrl = _site.ItemThumb.Match(block) is { Success: true } th ? AbsoluteHttps(WebUtility.HtmlDecode(First(th))) ?? SiteRelative(WebUtility.HtmlDecode(First(th))) : null,
                Duration = _site.ItemDuration?.Match(block) is { Success: true } d ? ParseClock(First(d)) : null,
                SourcePageUrl = _site.BaseUrl + id,
            });
        }

        next = _site.NextPage.Match(html) is { Success: true } n ? ToSitePath(WebUtility.HtmlDecode(First(n)), currentPath) : null;
        if (videos.Count == 0 && starts.Count > 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, $"{DisplayName} listing layout not recognized.");
        return videos;
    }

    internal IReadOnlyList<TubeCategory> ParseCategories(string html)
    {
        var cats = new List<TubeCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in _site.CategoryLink!.Matches(html))
        {
            if (ToSitePath(WebUtility.HtmlDecode(m.Groups[1].Value)) is not { } path)
                continue;
            var id = path.TrimStart('/');
            var name = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1))).Trim();
            if (name.Length == 0 || !_site.CategoryPath!.IsMatch(id) || !seen.Add(id))
                continue;
            cats.Add(new TubeCategory(id, name));
        }

        if (cats.Count == 0)
            throw new ProviderException(ProviderErrorKind.ParseChanged, $"{DisplayName} categories not found.");
        return cats;
    }

    internal static List<(Uri Url, int? Height)> ParseSourceTags(string html, bool includeContentUrl = false, Uri? page = null)
    {
        var list = new List<(Uri, int?)>();
        if (includeContentUrl)
        {
            foreach (Match m in ContentUrl.Matches(html))
            {
                var value = m.Groups[1].Value.Replace("\\/", "/", StringComparison.Ordinal);
                if (Uri.TryCreate(value, UriKind.Absolute, out var cu) && cu.Scheme == Uri.UriSchemeHttps
                    && (cu.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || cu.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)))
                    list.Add((cu, null));
            }
        }

        foreach (Match tag in SourceTag.Matches(html))
        {
            var attrs = Attr.Matches(tag.Value).ToDictionary(a => a.Groups[1].Value.ToLowerInvariant(), a => WebUtility.HtmlDecode(a.Groups[2].Value));
            if (!attrs.TryGetValue("src", out var src))
                continue;
            // "/path" must be treated as site-relative explicitly: on Linux, Uri.TryCreate(…, Absolute)
            // accepts it as file:///path (this broke HomeMoviesTube inside the Emby container).
            Uri? uri;
            if (src.StartsWith('/') && !src.StartsWith("//", StringComparison.Ordinal))
            {
                if (page is null || !Uri.TryCreate(page, src, out uri))
                    continue;
            }
            else if (!Uri.TryCreate(src.StartsWith("//", StringComparison.Ordinal) ? "https:" + src : src, UriKind.Absolute, out uri)
                && !(page is not null && Uri.TryCreate(page, src, out uri)))
            {
                continue;
            }
            if (uri.Scheme != Uri.UriSchemeHttps)
                continue;
            var label = attrs.GetValueOrDefault("label") ?? attrs.GetValueOrDefault("title") ?? attrs.GetValueOrDefault("size") ?? attrs.GetValueOrDefault("res");
            if (label is not null && label.Equals("auto", StringComparison.OrdinalIgnoreCase))
                continue;
            int? height = label is null ? null
                : label.Contains("4k", StringComparison.OrdinalIgnoreCase) || label.Trim() == "4" ? 2160 // TnAflix uses size="4" for 4K
                : Digits.Match(label) is { Success: true } d ? int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture)
                : null;
            list.Add((uri, height));
        }

        return list;
    }

    /// <summary>KVS flashvars: plain <c>video_url</c>/<c>video_alt_url[N]</c> plus their <c>_text</c> labels.</summary>
    internal static List<(Uri Url, int? Height)> ParseKvs(string html)
    {
        var list = new List<(Uri, int?)>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in KvsUrl.Matches(html))
            values.TryAdd(m.Groups[1].Value, m.Groups[2].Value);
        foreach (var (key, value) in values)
        {
            if (value.StartsWith("function/", StringComparison.Ordinal) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                continue; // obfuscated or not a URL: never decoded
            var label = Regex.Match(html, Regex.Escape(key) + @"_text\s*:\s*'([^']*)'", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            int? height = label.Success && label.Groups[1].Value.Contains("4k", StringComparison.OrdinalIgnoreCase) ? 2160
                : label.Success && Digits.Match(label.Groups[1].Value) is { Success: true } d ? int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture)
                : null;
            list.Add((uri, height));
        }

        return list;
    }

    private async Task<List<(Uri Url, int? Height)>> SubPageStreamsAsync(string videoId, CancellationToken ct)
    {
        var page = await GetAsync("/" + videoId, ct).ConfigureAwait(false);
        if (_site.SubPage?.Match(page) is not { Success: true } m || ToSitePath(WebUtility.HtmlDecode(m.Groups[1].Value)) is not { } sub)
            return new List<(Uri, int?)>();
        var html = await GetAsync(sub, ct).ConfigureAwait(false);
        var list = new List<(Uri, int?)>();
        foreach (Match x in Mp4Url.Matches(html))
        {
            if (Uri.TryCreate(x.Groups[1].Value, UriKind.Absolute, out var u))
                list.Add((u, null));
        }

        return list;
    }

    private async Task<List<(Uri Url, int? Height)>> DrTuberStreamsAsync(string videoId, CancellationToken ct)
    {
        var vid = DrTuberVid.Match(videoId) is { Success: true } m ? m.Groups[1].Value : throw new ProviderException(ProviderErrorKind.NotFound, "Invalid DrTuber id.");
        // The endpoint answers [] unless the request asks for JSON (plain content negotiation).
        var json = await _http.GetStringAsync(
            new Uri(_base, $"/player_config_json/?vid={vid}&aid=0&domain_id=0&embed=0&ref=null&check_speed=0"),
            AllowedHosts,
            ct,
            new Dictionary<string, string> { ["Accept"] = "application/json, text/javascript, */*; q=0.01" }).ConfigureAwait(false);
        var list = new List<(Uri, int?)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            // An unavailable video comes back as an empty array instead of an object.
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
            {
                foreach (var f in files.EnumerateObject())
                {
                    if (f.Value.ValueKind != JsonValueKind.String || !Uri.TryCreate(f.Value.GetString(), UriKind.Absolute, out var uri))
                        continue;
                    int? h = f.Name switch { "4k" => 2160, "hq" => 720, "lq" => 480, _ => null };
                    list.Add((uri, h));
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ProviderException(ProviderErrorKind.ParseChanged, "DrTuber player config is not valid JSON.", ex);
        }

        return list;
    }

    private async Task<List<(Uri Url, int? Height)>> TnaflixStreamsAsync(string videoId, CancellationToken ct)
    {
        var page = await GetAsync("/" + videoId, ct).ConfigureAwait(false);
        if (EmbedUrl.Match(page) is not { Success: true } e)
            return new List<(Uri, int?)>();
        var embed = e.Groups[1].Value.Replace("\\/", "/", StringComparison.Ordinal).Replace("/video/", "/ajax/video-player/", StringComparison.Ordinal);
        if (!Uri.TryCreate(embed, UriKind.Absolute, out var embedUri))
            return new List<(Uri, int?)>();
        var player = (await _http.GetStringAsync(embedUri, AllowedHosts, ct).ConfigureAwait(false))
            .Replace("\\/", "/", StringComparison.Ordinal).Replace("\\\"", "\"", StringComparison.Ordinal);
        return ParseSourceTags(player);
    }

    private async Task<Page<TubeVideo>> ListAsync(string path, CancellationToken ct)
    {
        string html;
        try
        {
            html = await GetAsync(path, ct).ConfigureAwait(false);
        }
        catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.NotFound && _site.SearchTemplate is { } t && path.StartsWith(t[..t.IndexOf('{', StringComparison.Ordinal)], StringComparison.Ordinal))
        {
            return Page<TubeVideo>.Empty;
        }

        var videos = ParseListing(html, out var next, path);
        if (next is null && videos.Count > 0)
            next = NextFromTemplate(path);
        return new Page<TubeVideo>(videos, next is not null && next != path && _site.ListingPath.IsMatch(next) ? next : null);
    }

    private string? NextFromTemplate(string path)
    {
        if (_site.PageTemplate is not { } template)
            return null;
        var pattern = "^" + Regex.Escape(template).Replace(@"\{n}", @"(\d+)", StringComparison.Ordinal) + "$";
        if (Regex.Match(path, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } m)
            return template.Replace("{n}", (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return path == _site.Sorts.First().Value ? template.Replace("{n}", "2", StringComparison.Ordinal) : null;
    }

    private string? SiteRelative(string value) =>
        value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) ? new Uri(_base, value).AbsoluteUri : null;

    private string? ToSitePath(string value, string? currentPath = null)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (currentPath is not null && !value.StartsWith('/') && !value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith('#') && !value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(new Uri(_base, currentPath), value, out var rel))
            value = rel.AbsoluteUri;
        if (value.StartsWith("//", StringComparison.Ordinal))
            value = "https:" + value;
        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var u) || !u.Host.Equals(_base.Host, StringComparison.OrdinalIgnoreCase))
                return null;
            return u.PathAndQuery + u.Fragment;
        }

        return value[0] == '/' && !value.Contains('\\') ? value : null;
    }

    private string ValidateListing(string token) =>
        ToSitePath(token) is { } p && _site.ListingPath.IsMatch(p)
            ? p
            : throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid page token.");

    /// <summary>First successful capture group (profiles may use alternatives like <c>(a)|(b)</c>).</summary>
    private static string First(Match m)
    {
        for (var i = 1; i < m.Groups.Count; i++)
        {
            if (m.Groups[i].Success)
                return m.Groups[i].Value;
        }

        return string.Empty;
    }

    private static string? AbsoluteHttps(string value) =>
        value.StartsWith("//", StringComparison.Ordinal) ? "https:" + value
        : value.StartsWith("http", StringComparison.Ordinal) ? value
        : null;

    private static TimeSpan? ParseClock(string text)
    {
        if (MinSec.Match(text) is { Success: true } ms)
        {
            var h = ms.Groups[1].Success ? int.Parse(ms.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            return new TimeSpan(h, int.Parse(ms.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(ms.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        if (Minutes.Match(text) is { Success: true } min)
            return TimeSpan.FromMinutes(int.Parse(min.Groups[1].Value, CultureInfo.InvariantCulture));
        var parts = text.Trim().Split(':');
        if (parts.Length is < 2 or > 3)
            return null;
        var total = 0;
        foreach (var p in parts)
        {
            if (!int.TryParse(p.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                return null;
            total = (total * 60) + n;
        }

        return TimeSpan.FromSeconds(total);
    }

    private Task<string> GetAsync(string path, CancellationToken ct) =>
        _http.GetStringAsync(new Uri(_base, path), AllowedHosts, ct);
}
