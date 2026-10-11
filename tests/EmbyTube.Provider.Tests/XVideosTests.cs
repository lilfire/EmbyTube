using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.XVideos;

namespace EmbyTube.Provider.Tests;

/// <summary>
/// Synthetic fixtures that mirror XVideos' markup structure (2026-10-11) with neutral
/// placeholder titles and fake URLs. No real site content is stored in the repository.
/// </summary>
internal static class XvFixtures
{
    public static string ListingClassic(int count, string? next) =>
        "<html><body><div class=\"pagination\"><ul>"
        + (next is null ? string.Empty : $"<li><a href=\"{next}\" class=\"no-page next-page\"><span>Next</span></a></li>")
        + "</ul></div><div id=\"content\"><div class=\"mozaique cust-nb-cols\">"
        + string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<div id=\"video_abc{i:D3}\" data-id=\"{1000 + i}\" class=\"frame-block thumb-block\"><div class=\"thumb-inside\"><div class=\"thumb\">"
            + $"<a href=\"/video.abc{i:D3}/sample_clip_{i}\"><img src=\"https://static.xvideos-cdn.com/blank.gif\"\n"
            + $"data-src=\"https://thumb.xvideos-cdn.com/x/{i}/xv_8_t.jpg\"\n"
            + $"data-sfwthumb=\"https://thumb.xvideos-cdn.com/x/{i}/xv_26_t.jpg\" />"
            + "<span class=\"top-right-tags\"><span class=\"video-hd-mark\">1080p</span></span></a></div></div>"
            + $"<div class=\"thumb-under\"><p class=\"title\"><a href=\"/video.abc{i:D3}/sample_clip_{i}\" title=\"Sample clip {i} &amp; more\">Sample clip {i} <span class=\"duration\">{i + 1} min</span></a></p>"
            + $"<p class=\"metadata\"><span class=\"bg\"><span class=\"duration\">{i + 1} min</span></span></p></div></div>\n"))
        + "</div></div></body></html>";

    public const string ListingNewLayout =
        "<div class=\"mozaique\"><div id=\"video-thumb-zz001\" class=\"thumb-block video\" data-video='{\"id\":1}'><div class=\"thumb\">"
        + "<a class=\"thumb-link\" href=\"/video.zz001/second_layout\" aria-label=\"Video\"><picture><img src=\"https://static.xvideos-cdn.com/b.gif\" data-src=\"https://thumb.xvideos-cdn.com/y/1.jpg\" alt=\"Video\" data-sfwthumb=\"https://thumb.xvideos-cdn.com/y/1s.jpg\"></picture></a>"
        + "<div class=\"thumb-overlay\"><span class=\"top-right\"><span class=\"video-hd-mark\">720p</span></span></div></div>"
        + "<div class=\"thumb-under\"><div class=\"title\" title=\"Second layout clip\"><a href=\"/video.zz001/second_layout\">Second layout clip</a></div>"
        + "<div class=\"video-metadata\"><span class=\"duration-container\"><span class=\"duration\">1 h 5 min</span></span></div></div></div></div>";

    public const string Home =
        "<ul><li><a href=\"/c/Sample_One-12\" class=\"btn\">Sample One</a></li><li><a href=\"/c/Beta-3\">Beta</a></li>"
        + "<li><a href=\"/c/Sample_One-12\">Sample One</a></li><li><a href=\"/c/Alpha-7\" >  Alpha &amp; Co </a></li></ul>";

    public const string VideoPage =
        "<html><head><meta property=\"og:title\" content=\"Sample video &quot;title&quot;\" />"
        + "<meta property=\"og:duration\" content=\"632\" /></head><body><script>"
        + "html5player.setVideoTitle('x');html5player.setVideoUrlLow('https://mp4.xvideos-cdn.com/v/low.mp4?secure=abc');"
        + "html5player.setVideoUrlHigh('https://mp4.xvideos-cdn.com/v/high.mp4?secure=abc');"
        + "html5player.setVideoHLS('https://hls.xvideos-cdn.com/v/hls.m3u8');"
        + "html5player.setThumbUrl169('https://thumb.xvideos-cdn.com/v/169.jpg');</script>"
        + "<script type=\"application/ld+json\">{\"uploadDate\": \"2026-10-09T22:00:00+00:00\"}</script></body></html>";

    public const string Master =
        "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1,RESOLUTION=854x480,NAME=\"480p\"\nhls-480p.m3u8\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=2,RESOLUTION=1920x1080,NAME=\"1080p\"\nhls-1080p.m3u8\n";
}

/// <summary>Routes requests by path to canned responses and records what was requested.</summary>
internal sealed class FakeSite : HttpMessageHandler
{
    public readonly List<Uri> Requests = new();
    private readonly Func<Uri, (HttpStatusCode, string)> _route;

    public FakeSite(Func<Uri, (HttpStatusCode, string)> route) => _route = route;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        var (status, body) = _route(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    public static FakeSite Default() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (u.Host.StartsWith("hls.", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XvFixtures.Master);
        if (pq == "/")
            return (HttpStatusCode.OK, XvFixtures.Home);
        if (pq.StartsWith("/video.abc000/", StringComparison.Ordinal) || pq.StartsWith("/video.abc001/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XvFixtures.VideoPage);
        if (pq.StartsWith("/video.", StringComparison.Ordinal))
            return (HttpStatusCode.NotFound, "gone");
        if (pq.Contains("k=zzzz-no-such-video-zzzz", StringComparison.Ordinal))
            return (HttpStatusCode.OK, "<div class=\"mozaique\"></div>No video match with this search.");
        if (pq.StartsWith("/best/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, pq.Count(c => c == '/') == 2
                ? XvFixtures.ListingNewLayout                      // /best/YYYY-MM
                : pq.EndsWith("/1", StringComparison.Ordinal)
                    ? XvFixtures.ListingClassic(3, null)           // /best/YYYY-MM/1
                    : "<div class=\"mozaique\"></div>");            // past the end
        if (pq == "/new/1" || pq.StartsWith("/c/", StringComparison.Ordinal) || pq.StartsWith("/?k=", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XvFixtures.ListingClassic(27, "/new/2"));
        if (pq == "/new/2")
            return (HttpStatusCode.OK, XvFixtures.ListingClassic(5, null));
        return (HttpStatusCode.NotFound, "not found");
    });
}

public class XVideosContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() =>
        new XVideosProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, FakeSite.Default()));

    protected override string PlayableVideoId => "abc000/sample_clip_0";
}

public class XVideosParserTests
{
    [Fact]
    public void Parses_classic_listing()
    {
        var listing = XVideosParser.ParseListing(XvFixtures.ListingClassic(3, "/new/2"), "xvideos", sfwThumbnails: false);

        Assert.Equal(3, listing.Videos.Count);
        var v = listing.Videos[1];
        Assert.Equal("abc001/sample_clip_1", v.Id);
        Assert.Equal("Sample clip 1 & more", v.Title);
        Assert.Equal("https://thumb.xvideos-cdn.com/x/1/xv_8_t.jpg", v.ThumbnailUrl);
        Assert.Equal(TimeSpan.FromMinutes(2), v.Duration);
        Assert.Equal(new[] { "1080p" }, v.Tags);
        Assert.Equal("/new/2", listing.NextPath);
    }

    [Fact]
    public void Parses_new_layout_and_sfw_thumbnails()
    {
        var listing = XVideosParser.ParseListing(XvFixtures.ListingNewLayout, "xvideos", sfwThumbnails: true);
        var v = Assert.Single(listing.Videos);
        Assert.Equal("zz001/second_layout", v.Id);
        Assert.Equal("Second layout clip", v.Title);
        Assert.Equal("https://thumb.xvideos-cdn.com/y/1s.jpg", v.ThumbnailUrl);
        Assert.Equal(TimeSpan.FromMinutes(65), v.Duration);
        Assert.Null(listing.NextPath);
    }

    [Fact]
    public void Unknown_layout_is_ParseChanged_not_empty()
    {
        var ex = Assert.Throws<ProviderException>(() => XVideosParser.ParseListing("<html><div class='new-grid'></div></html>", "xvideos", false));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
        Assert.Throws<ProviderException>(() => XVideosParser.ParseCategories("<html></html>"));
        Assert.Throws<ProviderException>(() => XVideosParser.ParseVideoPage("<html></html>", "xvideos", "a/b"));
    }

    [Fact]
    public void Empty_search_result_is_empty_page()
    {
        var listing = XVideosParser.ParseListing("<div class=\"mozaique\"></div>No video match with this search.", "xvideos", false);
        Assert.Empty(listing.Videos);
    }

    [Fact]
    public void Parses_categories_deduplicated_and_sorted()
    {
        var cats = XVideosParser.ParseCategories(XvFixtures.Home);
        Assert.Equal(new[] { "Alpha & Co", "Beta", "Sample One" }, cats.Select(c => c.Name));
        Assert.Equal("Alpha-7", cats[0].Id);
    }

    [Fact]
    public void Parses_video_page()
    {
        var page = XVideosParser.ParseVideoPage(XvFixtures.VideoPage, "xvideos", "abc000/x");
        Assert.Equal("Sample video \"title\"", page.Video.Title);
        Assert.Equal(TimeSpan.FromSeconds(632), page.Video.Duration);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 22, 0, 0, TimeSpan.Zero), page.Video.PublishedAt);
        Assert.Equal("hls.xvideos-cdn.com", page.Hls!.Host);
        Assert.EndsWith("high.mp4", page.Mp4High!.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("low.mp4", page.Mp4Low!.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10 min", 600)]
    [InlineData("45 sec", 45)]
    [InlineData("1 h 5 min", 3900)]
    [InlineData("2 h", 7200)]
    public void Parses_durations(string text, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), XVideosParser.ParseDuration(text));
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("")]
    public void Unknown_durations_are_null(string text) => Assert.Null(XVideosParser.ParseDuration(text));

    [Theory]
    [InlineData("/new/2", "/new/2")]
    [InlineData("/?k=a&p=1", "/?k=a&p=1")]
    [InlineData("https://evil.com/x", null)]
    [InlineData("//evil.com/x", null)]
    [InlineData("#1", null)]
    [InlineData("/\\evil", null)]
    public void Page_tokens_must_be_site_relative(string input, string? expected) =>
        Assert.Equal(expected, XVideosParser.NormalizePath(input));

    [Fact]
    public void Master_playlist_max_height() => Assert.Equal(1080, XVideosParser.MaxHeight(XvFixtures.Master));
}

public class XVideosProviderTests
{
    private static (XVideosProvider Provider, FakeSite Site) Create(FakeSite? site = null, Func<DateTimeOffset>? now = null)
    {
        site ??= FakeSite.Default();
        var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, site);
        return (new XVideosProvider(http, now: now ?? (() => new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero))), site);
    }

    [Fact]
    public async Task Popular_uses_previous_month_and_synthesizes_next_page()
    {
        var (p, site) = Create();
        var page = await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Popular }, CancellationToken.None);
        Assert.Equal("/best/2026-09", site.Requests[0].AbsolutePath);
        Assert.Equal("/best/2026-09/1", page.NextPageToken);

        var p2 = await p.BrowseAsync(new BrowseRequest { PageToken = "/best/2026-09/2" }, CancellationToken.None);
        Assert.Empty(p2.Items);
        Assert.False(p2.HasMore);
    }

    [Fact]
    public async Task January_popular_rolls_back_to_december()
    {
        var (p, site) = Create(now: () => new DateTimeOffset(2027, 1, 3, 0, 0, 0, TimeSpan.Zero));
        await p.BrowseAsync(new BrowseRequest(), CancellationToken.None);
        Assert.Equal("/best/2026-12", site.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task Newest_category_and_search_urls()
    {
        var (p, site) = Create();
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { CategoryId = "Alpha-7" }, CancellationToken.None);
        await p.SearchAsync(new SearchRequest("two words & more"), CancellationToken.None);
        Assert.Equal(new[] { "/new/1", "/c/Alpha-7", "/?k=two%20words%20%26%20more" }, site.Requests.Select(u => u.PathAndQuery));
    }

    [Fact]
    public async Task Invalid_category_ids_are_rejected_without_request()
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { CategoryId = "../account" }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public async Task Resolve_orders_hls_first_with_height_from_master()
    {
        var (p, _) = Create();
        var result = await p.ResolvePlaybackAsync("abc000/sample_clip_0", CancellationToken.None);
        Assert.Equal(new[] { StreamProtocol.Hls, StreamProtocol.Progressive, StreamProtocol.Progressive }, result.Variants.Select(v => v.Protocol));
        Assert.Equal(1080, result.Variants[0].Height);
        Assert.Equal(TimeSpan.FromSeconds(632), result.Duration);
    }

    [Fact]
    public async Task Withheld_streams_are_access_denied()
    {
        var site = new FakeSite(_ => (HttpStatusCode.OK, "<meta property=\"og:title\" content=\"x\" /><div>Please verify your age</div>"));
        var (p, _) = Create(site);
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("abc000/x", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.AccessDenied, ex.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, ProviderErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderErrorKind.Unavailable)]
    public async Task Http_errors_are_typed(HttpStatusCode status, ProviderErrorKind kind)
    {
        var (p, _) = Create(new FakeSite(_ => (status, "")));
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest(), CancellationToken.None));
        Assert.Equal(kind, ex.Kind);
    }

    [Fact]
    public async Task Categories_are_cached()
    {
        var (p, site) = Create();
        await p.GetCategoriesAsync(CancellationToken.None);
        await p.GetCategoriesAsync(CancellationToken.None);
        Assert.Single(site.Requests);
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("abc")]
    [InlineData("abc/def?x=1")]
    public async Task Malformed_video_ids_never_hit_the_network(string id)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync(id, CancellationToken.None));
        Assert.Null(await p.GetVideoAsync(id, CancellationToken.None));
        Assert.Empty(site.Requests);
    }
}

/// <summary>
/// Opt-in checks against the live site: set EMBYTUBE_LIVE_TESTS=1. Prints counts and
/// hosts only. Not run in CI.
/// </summary>
[Trait("Category", "Live")]
public class XVideosLiveTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") == "1";

    [Fact]
    public async Task Live_end_to_end()
    {
        if (!Enabled)
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1) });
        var p = new XVideosProvider(http);
        var ct = CancellationToken.None;

        var cats = await p.GetCategoriesAsync(ct);
        Assert.True(cats.Count >= 10, $"categories: {cats.Count}");

        foreach (var request in new[]
                 {
                     new BrowseRequest { Sort = TubeSorts.Popular },
                     new BrowseRequest { Sort = TubeSorts.Newest },
                     new BrowseRequest { CategoryId = cats[0].Id },
                 })
        {
            var page = await p.BrowseAsync(request, ct);
            Assert.True(page.Items.Count >= 10, $"{request.Sort ?? request.CategoryId}: {page.Items.Count}");
            Assert.True(page.HasMore);
            Assert.All(page.Items, v => Assert.NotNull(v.ThumbnailUrl));
            var next = await p.BrowseAsync(request with { PageToken = page.NextPageToken }, ct);
            Assert.NotEqual(page.Items[0].Id, next.Items[0].Id);
        }

        var search = await p.SearchAsync(new SearchRequest("amateur"), ct);
        Assert.NotEmpty(search.Items);

        var first = (await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, ct)).Items[0];
        var result = await p.ResolvePlaybackAsync(first.Id, ct);
        Assert.Contains(result.Variants, v => v.Protocol == StreamProtocol.Hls);
        Assert.All(result.Variants, v => Assert.EndsWith("xvideos-cdn.com", v.Url.Host, StringComparison.Ordinal));
    }
}
