using System.Net;
using System.Text.Json;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.XHamster;

namespace EmbyTube.Provider.Tests;

/// <summary>
/// Synthetic pages mirroring xHamster's structure (2026-10-11): page state as JSON in
/// <c>window.initials</c>. Neutral placeholder content and fake URLs only.
/// </summary>
internal static class XhFixtures
{
    public static string Page(object initials, string extraHead = "") =>
        $"<html><head>{extraHead}</head><body><script>window.initials={JsonSerializer.Serialize(initials)};</script></body></html>";

    public static object Thumb(int i, bool geoBlocked = false) => new Dictionary<string, object?>
    {
        ["id"] = 1000 + i,
        ["duration"] = 60 * (i + 1),
        ["created"] = 1_791_720_090 - i,
        ["title"] = $"Sample clip {i} &amp; more",
        ["pageURL"] = $"https://xhamster.com/videos/sample-clip-{i}-xhA{i}",
        ["thumbURL"] = $"https://ic-vt-nss.xhcdn.com/t/{i}.jpg",
        ["isHD"] = i % 2 == 0,
        ["isUHD"] = i == 3,
        ["isBlockedByGeo"] = geoBlocked,
    };

    public static string Listing(int count, int current, int last, string template, int blockedExtra = 0) => Page(new
    {
        layoutPage = new
        {
            videoListProps = new { videoThumbProps = Enumerable.Range(0, count).Select(i => Thumb(i)).Concat(Enumerable.Range(100, blockedExtra).Select(i => Thumb(i, geoBlocked: true))) },
            paginationProps = new { currentPageNumber = current, lastPageNumber = last, pageLinkTemplate = template },
        },
    });

    public static string Search(int count, int next, int maxPages, string query) => Page(new
    {
        searchResult = new { videoThumbProps = Enumerable.Range(0, count).Select(i => Thumb(i + 50)) },
        pagination = new { next, maxPages, pageLinkTemplate = $"https://xhamster.com/search/{query}?page={{#}}" },
    });

    public static readonly string Home = Page(new
    {
        layoutPage = new
        {
            sidebarProps = new
            {
                items = new object[]
                {
                    new { name = "4K Porn", url = "https://xhamster.com/4k?formatFrozen=1" },
                    new { name = "Alpha", url = "https://xhamster.com/categories/alpha" },
                    new { name = "Beta &amp; Co", url = "https://xhamster.com/categories/beta-co" },
                    new { name = "Alpha", url = "https://xhamster.com/categories/alpha" },
                    new { name = "Tag", url = "https://xhamster.com/tags/porn" },
                    new { name = "Evil", url = "https://evil.com/categories/x" },
                },
            },
        },
    });

    public static readonly string VideoPage = Page(
        new { videoModel = new { title = "Sample video", description = "Synthetic", duration = 132, created = 1_791_720_090, thumbURL = "https://ic-vt-nss.xhcdn.com/v.jpg" } },
        "<link rel=\"preload\" href=\"https://video-nss.xhcdn.com/abc/master.h264.m3u8?token=x\" as=\"fetch\">");

    public static readonly string VideoPageObfuscatedOnly = Page(new
    {
        videoModel = new { title = "Sample video", duration = 10 },
        xplayerSettings = new { sources = new { hls = new { h264 = new { url = "6f6266757363617465642d6865782d737472696e67" } } } },
    });

    public const string Master =
        "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1,RESOLUTION=854x480\n480p.h264.mp4.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=2,RESOLUTION=1280x720\n720p.h264.mp4.m3u8\n";
}

public class XHamsterParserTests
{
    [Fact]
    public void Parses_listing_and_skips_geo_blocked()
    {
        var listing = XHamsterParser.ParseListing(XhFixtures.Listing(4, 1, 10, "https://xhamster.com/newest/{#}", blockedExtra: 2), "xhamster");
        Assert.Equal(4, listing.Videos.Count);
        var v = listing.Videos[3];
        Assert.Equal("sample-clip-3-xhA3", v.Id);
        Assert.Equal("Sample clip 3 & more", v.Title);
        Assert.Equal(TimeSpan.FromMinutes(4), v.Duration);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_791_720_087), v.PublishedAt);
        Assert.Equal(new[] { "4K" }, v.Tags);
        Assert.Equal("/newest/2", listing.NextPath);
    }

    [Fact]
    public void Last_page_has_no_next()
    {
        Assert.Null(XHamsterParser.ParseListing(XhFixtures.Listing(2, 10, 10, "/best/{#}"), "xhamster").NextPath);
    }

    [Fact]
    public void Search_pagination_uses_next_field()
    {
        var listing = XHamsterParser.ParseListing(XhFixtures.Search(3, 2, 96, "abc"), "xhamster");
        Assert.Equal(3, listing.Videos.Count);
        Assert.Equal("/search/abc?page=2", listing.NextPath);
    }

    [Fact]
    public void Categories_come_from_sidebar_only_category_links()
    {
        var cats = XHamsterParser.ParseCategories(XhFixtures.Home);
        Assert.Equal(new[] { ("alpha", "Alpha"), ("beta-co", "Beta & Co") }, cats.Select(c => (c.Id, c.Name)));
    }

    [Fact]
    public void Video_page_uses_plain_preload_hls()
    {
        var page = XHamsterParser.ParseVideoPage(XhFixtures.VideoPage, "xhamster", "sample-xh1");
        Assert.False(page.Deleted);
        Assert.Equal("video-nss.xhcdn.com", page.Hls!.Host);
        Assert.Equal("Sample video", page.Video!.Title);
        Assert.Equal(TimeSpan.FromSeconds(132), page.Video.Duration);
    }

    [Fact]
    public void Obfuscated_player_urls_are_not_used()
    {
        var page = XHamsterParser.ParseVideoPage(XhFixtures.VideoPageObfuscatedOnly, "xhamster", "sample-xh1");
        Assert.Null(page.Hls);
        Assert.NotNull(page.Video);
    }

    [Fact]
    public void Deleted_video_is_detected()
    {
        Assert.True(XHamsterParser.ParseVideoPage("<html>This video was deleted</html>", "xhamster", "x").Deleted);
    }

    [Fact]
    public void Missing_page_state_is_ParseChanged()
    {
        var ex = Assert.Throws<ProviderException>(() => XHamsterParser.ParseListing("<html></html>", "xhamster"));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
        ex = Assert.Throws<ProviderException>(() => XHamsterParser.ParseListing("<script>window.initials={not json};</script>", "xhamster"));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
        Assert.Throws<ProviderException>(() => XHamsterParser.ParseCategories(XhFixtures.Page(new { layoutPage = new { } })));
    }

    [Theory]
    [InlineData("https://xhamster.com/videos/some-title-xh1AbC2", "some-title-xh1AbC2")]
    [InlineData("https://de.xhamster.com/videos/abc-xh9", "abc-xh9")]
    [InlineData("https://evil.com/videos/abc", null)]
    [InlineData("https://xhamster.com/videos/a/b", null)]
    [InlineData("https://xhamster.com/embed/abc", null)]
    public void Video_ids_from_urls(string url, string? expected) => Assert.Equal(expected, XHamsterParser.VideoIdFromUrl(url));

    [Theory]
    [InlineData("https://xhamster.com/newest/2", "/newest/2")]
    [InlineData("/search/a?page=3", "/search/a?page=3")]
    [InlineData("https://evil.com/x", null)]
    [InlineData("//evil.com/x", null)]
    [InlineData("/newest/{#}", null)]
    public void Page_tokens_are_site_relative(string input, string? expected) => Assert.Equal(expected, XHamsterParser.NormalizePath(input));
}

internal static class XhSite
{
    public static FakeSite Default() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (u.Host == "video-nss.xhcdn.com")
            return (HttpStatusCode.OK, XhFixtures.Master);
        if (pq == "/")
            return (HttpStatusCode.OK, XhFixtures.Home);
        if (pq == "/best")
            return (HttpStatusCode.OK, XhFixtures.Listing(5, 1, 2, "https://xhamster.com/best/{#}"));
        if (pq == "/best/2")
            return (HttpStatusCode.OK, XhFixtures.Page(new { layoutPage = new { videoListProps = new { videoThumbProps = new[] { XhFixtures.Thumb(9) } }, paginationProps = new { currentPageNumber = 2, lastPageNumber = 2, pageLinkTemplate = "/best/{#}" } } }));
        if (pq == "/newest" || pq.StartsWith("/categories/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XhFixtures.Listing(5, 1, 50, "https://xhamster.com" + pq + "/{#}"));
        if (pq.StartsWith("/search/zzzz", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XhFixtures.Page(new { searchResult = new { videoThumbProps = Array.Empty<object>() } }));
        if (pq.StartsWith("/search/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, XhFixtures.Search(3, pq.Contains("page=10") ? 11 : 2, 96, "abc"));
        if (pq == "/videos/sample-clip-0-xhA0")
            return (HttpStatusCode.OK, XhFixtures.VideoPage);
        if (pq == "/videos/obfuscated-xh1")
            return (HttpStatusCode.OK, XhFixtures.VideoPageObfuscatedOnly);
        if (pq == "/videos/deleted-xh2")
            return (HttpStatusCode.OK, "<html>This video was deleted</html>");
        return (HttpStatusCode.NotFound, "not found");
    });
}

public class XHamsterContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() =>
        new XHamsterProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, XhSite.Default()));

    protected override string PlayableVideoId => "sample-clip-0-xhA0";
}

public class XHamsterProviderTests
{
    private static (XHamsterProvider Provider, FakeSite Site) Create(FakeSite? site = null)
    {
        site ??= XhSite.Default();
        return (new XHamsterProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, site)), site);
    }

    [Fact]
    public async Task Urls_for_sorts_categories_and_search()
    {
        var (p, site) = Create();
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Popular }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { CategoryId = "alpha" }, CancellationToken.None);
        await p.SearchAsync(new SearchRequest("two words"), CancellationToken.None);
        Assert.Equal(new[] { "/best", "/newest", "/categories/alpha", "/search/two%20words" }, site.Requests.Select(u => u.PathAndQuery));
    }

    [Fact]
    public async Task Search_stops_after_page_10_per_robots_txt()
    {
        var (p, _) = Create();
        var page = await p.SearchAsync(new SearchRequest("abc") { PageToken = "/search/abc?page=10" }, CancellationToken.None);
        Assert.False(page.HasMore);
        var first = await p.SearchAsync(new SearchRequest("abc"), CancellationToken.None);
        Assert.Equal("/search/abc?page=2", first.NextPageToken);
    }

    [Fact]
    public async Task Resolve_returns_hls_with_height()
    {
        var (p, _) = Create();
        var r = await p.ResolvePlaybackAsync("sample-clip-0-xhA0", CancellationToken.None);
        var v = Assert.Single(r.Variants);
        Assert.Equal(StreamProtocol.Hls, v.Protocol);
        Assert.Equal(720, v.Height);
        Assert.Equal(TimeSpan.FromSeconds(132), r.Duration);
    }

    [Fact]
    public async Task Obfuscated_only_is_unsupported_and_deleted_is_not_found()
    {
        var (p, site) = Create();
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("obfuscated-xh1", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Unsupported, ex.Kind);
        ex = await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("deleted-xh2", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.NotFound, ex.Kind);
        Assert.DoesNotContain(site.Requests, u => u.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../account")]
    [InlineData("Alpha")]
    [InlineData("a/b")]
    public async Task Invalid_category_ids_never_hit_the_network(string id)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { CategoryId = id }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public async Task Categories_are_cached()
    {
        var (p, site) = Create();
        await p.GetCategoriesAsync(CancellationToken.None);
        await p.GetCategoriesAsync(CancellationToken.None);
        Assert.Single(site.Requests);
    }
}

/// <summary>Opt-in live check: EMBYTUBE_LIVE_TESTS=1. Prints counts only.</summary>
[Trait("Category", "Live")]
public class XHamsterLiveTests
{
    [Fact]
    public async Task Live_end_to_end()
    {
        if (Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") != "1")
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1) });
        var p = new XHamsterProvider(http);
        var ct = CancellationToken.None;

        var cats = await p.GetCategoriesAsync(ct);
        Assert.True(cats.Count >= 20, $"categories: {cats.Count}");

        foreach (var request in new[]
                 {
                     new BrowseRequest { Sort = TubeSorts.Popular },
                     new BrowseRequest { Sort = TubeSorts.Newest },
                     new BrowseRequest { CategoryId = cats[^1].Id },
                 })
        {
            var page = await p.BrowseAsync(request, ct);
            Assert.True(page.Items.Count >= 10, $"{request.Sort ?? request.CategoryId}: {page.Items.Count}");
            Assert.True(page.HasMore);
            var next = await p.BrowseAsync(request with { PageToken = page.NextPageToken }, ct);
            Assert.NotEqual(page.Items[0].Id, next.Items[0].Id);
        }

        var search = await p.SearchAsync(new SearchRequest("amateur"), ct);
        Assert.NotEmpty(search.Items);
        Assert.Equal("/search/amateur?page=2", search.NextPageToken);

        var newest = (await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, ct)).Items;
        var resolved = 0;
        foreach (var v in newest.Take(3))
        {
            try
            {
                var r = await p.ResolvePlaybackAsync(v.Id, ct);
                Assert.All(r.Variants, x => Assert.EndsWith("xhcdn.com", x.Url.Host, StringComparison.Ordinal));
                resolved++;
            }
            catch (ProviderException ex) when (ex.Kind is ProviderErrorKind.Unsupported or ProviderErrorKind.NotFound)
            {
            }
        }

        Assert.True(resolved >= 2, $"resolved {resolved}/3");
    }
}
