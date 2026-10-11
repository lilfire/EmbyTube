using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.Pornhub;

namespace EmbyTube.Provider.Tests;

/// <summary>Synthetic Pornhub-structured fixtures (2026-10-11 markup), neutral content, fake URLs.</summary>
internal static class PhFixtures
{
    private static string Item(string key, string title, bool premium = false) =>
        $"<li class=\"pcVideoListItem js-pop videoblock videoBox\" id=\"v1\" data-video-id=\"1\" data-video-vkey=\"{key}\" data-id=\"1\">"
        + $"<div class=\"phimage\"><a href=\"/view_video.php?viewkey={key}\" title=\"{title}\" class=\"linkVideoThumb\">"
        + $"<img src=\"https://ei.phncdn.com/b.gif\" data-mediumthumb=\"https://ei.phncdn.com/t/{key}.jpg\" />"
        + "<div class=\"marker-overlays js-noFade\"><var class=\"duration\">15:12</var></div></a></div>"
        + (premium ? "<span class=\"premiumIcon\"></span>" : string.Empty)
        + "</li>";

    public static string Listing(string listId, int count, string? next) =>
        "<ul class=\"dropdownHottestVideos videos\" id=\"hottestMenuSection\">" + Item("hot1", "Menu item") + "</ul>"
        + $"<ul id=\"{listId}\" class=\"nf-videos videos search-video-thumbs\">"
        + string.Concat(Enumerable.Range(0, count).Select(i => Item($"k{listId.Length}x{i}", $"Sample clip {i} &amp; more")))
        + Item("prem1", "Premium only", premium: true)
        + "</ul><ul class=\"videos premiumStyle full-row-thumbs\">" + Item("tail1", "Tail item") + "</ul>"
        + (next is null ? string.Empty : $"<li class=\"page_next\"><a href=\"{next}\" class=\"orangeButton\">Next</a></li>");

    public const string Categories =
        "<li class=\"catPic\"><div class=\"category-wrapper\"><div class=\"relativeWrapper phimage\"><a href=\"/categories/alpha\" alt=\"Alpha\" ><img src=\"https://ei.phncdn.com/c.jpg\"/></a></div></div></li>"
        + "<li class=\"catPic\"><div class=\"category-wrapper \"><div class=\"relativeWrapper\"><a href=\"/categories/beta-co\" alt=\"Beta &amp; Co\" ></a></div></div></li>"
        + "<li class=\"catPic\"><div class=\"category-wrapper\"><a href=\"/categories/alpha\" alt=\"Alpha\" ></a></div></li>"
        + "<li class=\"catPic\"><div class=\"category-wrapper\"><a href=\"/gayporn\" alt=\"Gay\" ></a></div></li>"
        + "<li class=\"catPic\"><div class=\"category-wrapper\"><a href=\"/video?c=492\" alt=\"Numbered\"\n   ></a></div></li>";

    public const string VideoPage =
        "<script>var flashvars_1 = {\"video_duration\":912,\"video_title\":\"Sample video \\u0026 title\",\"image_url\":\"https:\\/\\/ei.phncdn.com\\/v.jpg\","
        + "\"mediaDefinitions\":[{\"defaultQuality\":false,\"format\":\"hls\",\"videoUrl\":\"https:\\/\\/hv-h.phncdn.com\\/hls\\/v\\/1080P.mp4\\/master.m3u8?validto=1\",\"quality\":\"1080\"},"
        + "{\"format\":\"hls\",\"videoUrl\":\"https:\\/\\/hv-h.phncdn.com\\/hls\\/v\\/720P.mp4\\/master.m3u8?validto=1\",\"quality\":\"720\"},"
        + "{\"format\":\"mp4\",\"videoUrl\":\"https:\\/\\/www.pornhub.com\\/video\\/get_media?s=x\",\"quality\":[],\"remote\":true}],\"isVertical\":\"false\"};</script>"
        + "<script type=\"application/ld+json\">{\"uploadDate\": \"2026-10-07T10:00:00+00:00\"}</script>";

    public static FakeSite Site() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (pq == "/categories")
            return (HttpStatusCode.OK, Categories);
        if (pq == "/video?o=cm" || pq == "/video?o=mv" || pq == "/video?c=492" || pq.StartsWith("/categories/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing("videoCategory", 4, pq.Contains('?') ? pq + "&amp;page=2" : pq + "?page=2"));
        if (pq.EndsWith("page=2", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing("videoCategory", 2, null).Replace("k13x", "p2x", StringComparison.Ordinal));
        if (pq.Contains("search=zzzz-no-such-video", StringComparison.Ordinal))
            return (HttpStatusCode.NotFound, "<html>Page not found</html>");
        if (pq == "/video/search?search=abc&page=9")
            return (HttpStatusCode.OK, Listing("videoSearchResult", 3, "/video/search?search=abc&amp;page=10"));
        if (pq.StartsWith("/video/search?search=", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing("videoSearchResult", 3, "/video/search?search=abc&amp;page=2"));
        if (pq == "/view_video.php?viewkey=k13x0")
            return (HttpStatusCode.OK, VideoPage);
        if (pq == "/view_video.php?viewkey=gated1")
            return (HttpStatusCode.OK, "<script>var flashvars = {\"video_title\":\"Gated\",\"mediaDefinitions\":[]};</script>");
        return (HttpStatusCode.NotFound, "not found");
    });
}

public class PornhubContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() =>
        new PornhubProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, PhFixtures.Site()), "EmbyTube/test");

    protected override string PlayableVideoId => "k13x0";
}

public class PornhubTests
{
    private static (PornhubProvider P, FakeSite Site) Create()
    {
        var site = PhFixtures.Site();
        return (new PornhubProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, site), "EmbyTube/test"), site);
    }

    [Fact]
    public void Reads_only_main_list_and_skips_premium()
    {
        var listing = PornhubParser.ParseListing(PhFixtures.Listing("videoCategory", 3, "/video?o=cm&amp;page=2"), "pornhub");
        Assert.Equal(new[] { "k13x0", "k13x1", "k13x2" }, listing.Videos.Select(v => v.Id));
        var v = listing.Videos[0];
        Assert.Equal("Sample clip 0 & more", v.Title);
        Assert.Equal(TimeSpan.FromSeconds(912), v.Duration);
        Assert.Equal("https://ei.phncdn.com/t/k13x0.jpg", v.ThumbnailUrl);
        Assert.Equal("/video?o=cm&page=2", listing.NextPath);
    }

    [Fact]
    public void No_results_page_is_empty_and_unknown_layout_is_ParseChanged()
    {
        Assert.Empty(PornhubParser.ParseListing("<div class=\"noResultsWrapper\"></div>", "pornhub").Videos);
        var ex = Assert.Throws<ProviderException>(() => PornhubParser.ParseListing("<html>new layout</html>", "pornhub"));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
    }

    [Fact]
    public void Parses_categories()
    {
        var cats = PornhubParser.ParseCategories(PhFixtures.Categories);
        Assert.Equal(new[] { ("alpha", "Alpha"), ("beta-co", "Beta & Co"), ("c492", "Numbered") }, cats.Select(c => (c.Id, c.Name)));
    }

    [Fact]
    public void Parses_video_page_hls_only()
    {
        var page = PornhubParser.ParseVideoPage(PhFixtures.VideoPage, "pornhub", "k13x0");
        Assert.Equal(new int?[] { 1080, 720 }, page.Hls.Select(s => s.Height));
        Assert.All(page.Hls, s => Assert.Equal("hv-h.phncdn.com", s.Url.Host));
        Assert.Equal("Sample video & title", page.Video!.Title);
        Assert.Equal(TimeSpan.FromSeconds(912), page.Video.Duration);
        Assert.Equal("https://ei.phncdn.com/v.jpg", page.Video.ThumbnailUrl);
    }

    [Theory]
    [InlineData("15:12", 912)]
    [InlineData("1:02:03", 3723)]
    public void Parses_clock_durations(string text, int seconds) => Assert.Equal(TimeSpan.FromSeconds(seconds), PornhubParser.ParseClock(text));

    [Fact]
    public async Task Urls_respect_robots_txt()
    {
        var (p, site) = Create();
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Popular }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { CategoryId = "alpha" }, CancellationToken.None);
        var numbered = await p.BrowseAsync(new BrowseRequest { CategoryId = "c492" }, CancellationToken.None);
        Assert.Equal("/video?c=492&page=2", numbered.NextPageToken);
        var search = await p.SearchAsync(new SearchRequest("two words"), CancellationToken.None);
        Assert.Equal(new[] { "/video?o=cm", "/video?o=mv", "/categories/alpha", "/video?c=492", "/video/search?search=two+words" }, site.Requests.Select(u => u.PathAndQuery));
        Assert.All(site.Requests, u => Assert.DoesNotMatch("cc=|hd=1|min_duration|max_duration|[?&]o=.*search|search.*[?&]o=", u.PathAndQuery));

        var last = await p.SearchAsync(new SearchRequest("abc") { PageToken = "/video/search?search=abc&page=9" }, CancellationToken.None);
        Assert.False(last.HasMore); // page 10 is disallowed
        Assert.Equal("/video/search?search=abc&page=2", search.NextPageToken);
    }

    [Theory]
    [InlineData("/view_video.php?viewkey=abc")]
    [InlineData("/video/search?search=a&o=mr")]
    [InlineData("/video?o=cm&cc=us")]
    [InlineData("https://evil.com/video?o=cm")]
    public async Task Tokens_outside_allowed_listings_are_rejected(string token)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { PageToken = token }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public async Task Resolve_adds_referer_and_own_user_agent_never_cookies()
    {
        var (p, _) = Create();
        var r = await p.ResolvePlaybackAsync("k13x0", CancellationToken.None);
        Assert.Equal(2, r.Variants.Count);
        Assert.All(r.Variants, v =>
        {
            Assert.Equal(StreamProtocol.Hls, v.Protocol);
            Assert.Equal("https://www.pornhub.com/", v.Headers["Referer"]);
            Assert.Equal("EmbyTube/test", v.Headers["User-Agent"]);
            Assert.False(v.Headers.ContainsKey("Cookie"));
        });
    }

    [Fact]
    public async Task Withheld_streams_are_access_denied()
    {
        var (p, _) = Create();
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("gated1", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.AccessDenied, ex.Kind);
    }
}

[Trait("Category", "Live")]
public class PornhubLiveTests
{
    [Fact]
    public async Task Live_end_to_end()
    {
        if (Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") != "1")
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1) });
        var p = new PornhubProvider(http, "EmbyTube/0.1");
        var ct = CancellationToken.None;

        var cats = await p.GetCategoriesAsync(ct);
        Assert.True(cats.Count >= 20, $"categories: {cats.Count}");

        foreach (var request in new[]
                 {
                     new BrowseRequest { Sort = TubeSorts.Popular },
                     new BrowseRequest { Sort = TubeSorts.Newest },
                     new BrowseRequest { CategoryId = cats[3].Id },
                 })
        {
            var page = await p.BrowseAsync(request, ct);
            Assert.True(page.Items.Count >= 10, $"{request.Sort ?? request.CategoryId}: {page.Items.Count}");
            Assert.True(page.HasMore, $"{request.Sort ?? request.CategoryId}: no next page");
            var next = await p.BrowseAsync(request with { PageToken = page.NextPageToken }, ct);
            Assert.NotEqual(page.Items[0].Id, next.Items[0].Id);
        }

        var search = await p.SearchAsync(new SearchRequest("amateur"), ct);
        Assert.True(search.Items.Count >= 10, $"search: {search.Items.Count}");

        var r = await p.ResolvePlaybackAsync(search.Items[0].Id, ct);
        Assert.NotEmpty(r.Variants);
        Assert.All(r.Variants, v => Assert.EndsWith("phncdn.com", v.Url.Host, StringComparison.Ordinal));

        Assert.All(r.Variants, v => Assert.Equal("https://www.pornhub.com/", v.Headers["Referer"]));

        // Not asserted: whether the CDN serves the stream to a non-browser client. One of
        // Pornhub's CDN variants (hv-h.phncdn.com, ?h=&e=&f=) answers 410 to .NET and ffmpeg
        // while curl gets 200 with identical URL and headers (client fingerprinting). EmbyTube
        // does not imitate other clients; see docs/sites/pornhub.md.
    }
}
