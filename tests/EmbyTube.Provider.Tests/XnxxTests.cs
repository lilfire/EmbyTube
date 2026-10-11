using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.XVideos;

namespace EmbyTube.Provider.Tests;

/// <summary>Synthetic XNXX-structured fixtures (2026-10-11 markup), neutral content, fake URLs.</summary>
internal static class XnFixtures
{
    public static string Listing(int count, int activePage, bool morePages) =>
        "<div class=\"mozaique\">"
        + string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<div id=\"video_e{activePage}x{i}\" data-id=\"{i}\" class=\"thumb-block with-uploader\"><div class=\"thumb-inside\"><div class=\"thumb\">"
            + $"<a href=\"/video-e{activePage}x{i}/5683{i}/0/sample_clip_{i}\"><img src=\"https://static.xnxx-cdn.com/b.gif\" data-src=\"https://thumb.xnxx-cdn.com/t/{i}.jpg\" data-sfwthumb=\"https://thumb.xnxx-cdn.com/t/{i}s.jpg\" /></a></div></div>"
            + $"<div class=\"thumb-under\"><p><a href=\"/video-e{activePage}x{i}/5683{i}/0/sample_clip_{i}\" title=\"Sample clip {i}\">Sample clip {i}</a></p>"
            + "<p class=\"metadata\"><span class=\"right\"> 28.8k <span class=\"icon-f icf-eye\"></span><span class=\"superfluous\">86%</span></span> 10min <span class=\"video-hd\"><span class=\"superfluous\"> - </span> 1080p </span></p></div></div>\n"))
        + "</div><div class=\"pagination \"><ul>"
        + (activePage > 0 ? $"<li><a href=\"/search/abc/{activePage - 1}\">{activePage}</a></li>" : string.Empty)
        + $"<li><a class=\"active\" href=\"\">{activePage + 1}</a></li>"
        + (morePages ? $"<li><a href=\"/search/abc/{activePage + 1}\">{activePage + 2}</a></li>" : string.Empty)
        + "</ul></div>";

    public const string BestLayout =
        "<div class=\"mozaique\"><div id=\"video-thumb-bb1\" class=\"thumb-block video\" data-video='{\"id\":1}'><div class=\"thumb\">"
        + "<a class=\"thumb-link\" href=\"/video-bb1/best_clip\" aria-label=\"Video\"><img src=\"https://static.xnxx-cdn.com/b.gif\" data-src=\"https://thumb.xnxx-cdn.com/b/1.jpg\" alt=\"Video\"></a></div>"
        + "<div class=\"thumb-under\"><a class=\"title\" href=\"/video-bb1/best_clip\" title=\"Best clip\">Best clip</a>"
        + "<div class=\"metadata\"><span class=\"left\"> 13min <span class=\"video-hd\"><span class=\"superfluous\"> - </span> 1080p </span></span></div></div></div></div>"
        + "<div class=\"pagination \"> <a class=\"current\" href=\"\" data-page=\"0\">1</a> <a class=\"can_hide\" href=\"/best/2026-09/1\" data-page=\"1\">2</a></div>";

    public const string Home =
        "<script>cats.write_thumb_block_list([{\"tf\":\"Today&#039;s selection\",\"u\":\"/todays-selection\"},"
        + "{\"tf\":\"Alpha\",\"u\":\"/search/alpha?id=123\"},{\"tf\":\"Alpha dup\",\"u\":\"/search/alpha?id=123\"},"
        + "{\"tf\":\"Profile\",\"u\":\"/account/x\"},{\"tf\":\"Evil\",\"u\":\"https://evil.com/search/x\"},{\"u\":\"/search/noname\"}], 'x');</script>";

    public const string VideoPage =
        "<meta property=\"og:title\" content=\"Sample XNXX video\" /><meta property=\"og:duration\" content=\"609\" />"
        + "<script>html5player.setVideoUrlLow('https://mp4.xnxx-cdn.com/v/low.mp4?secure=a');html5player.setVideoUrlHigh('https://mp4.xnxx-cdn.com/v/high.mp4?secure=a');"
        + "html5player.setVideoHLS('https://hls.xnxx-cdn.com/v/hls.m3u8');html5player.setThumbUrl169('https://thumb.xnxx-cdn.com/v/169.jpg');</script>";

    public static FakeSite Site() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (u.Host.StartsWith("hls.", StringComparison.Ordinal))
            return (HttpStatusCode.OK, "#EXTM3U\n#EXT-X-STREAM-INF:RESOLUTION=1280x720\nhls-720p.m3u8\n");
        if (pq == "/")
            return (HttpStatusCode.OK, Home);
        if (pq.StartsWith("/best/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, pq.Count(c => c == '/') == 2 ? BestLayout : "<div class=\"mozaique\"></div>");
        if (pq == "/todays-selection")
            return (HttpStatusCode.OK, Listing(3, 0, morePages: false).Replace("<div class=\"pagination \"><ul><li><a class=\"active\" href=\"\">1</a></li></ul></div>", string.Empty, StringComparison.Ordinal));
        if (pq.StartsWith("/todays-selection/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, "<div class=\"mozaique\"></div>");
        if (pq.Contains("zzzz-no-such-video", StringComparison.Ordinal))
            return (HttpStatusCode.OK, "<div class=\"mozaique\"></div>No video match with this search.");
        if (pq == "/search/abc/1")
            return (HttpStatusCode.OK, Listing(4, 1, morePages: false));
        if (pq.StartsWith("/search/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing(5, 0, morePages: true));
        if (pq.StartsWith("/video-e0x0/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, VideoPage);
        return (HttpStatusCode.NotFound, "not found");
    });
}

public class XnxxContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() =>
        new XnxxProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, XnFixtures.Site()));

    protected override string PlayableVideoId => "e0x0/56830/0/sample_clip_0";
}

public class XnxxTests
{
    private static (XnxxProvider P, FakeSite Site) Create()
    {
        var site = XnFixtures.Site();
        return (new XnxxProvider(new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, site),
            now: () => new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero)), site);
    }

    [Fact]
    public void Parses_xnxx_listing_with_text_duration_and_quality()
    {
        var listing = XVideosParser.ParseListing(XnFixtures.Listing(2, 0, morePages: true), "xnxx", false, XvSite.Xnxx);
        var v = listing.Videos[1];
        Assert.Equal("e0x1/56831/0/sample_clip_1", v.Id);
        Assert.Equal("Sample clip 1", v.Title);
        Assert.Equal(TimeSpan.FromMinutes(10), v.Duration);
        Assert.Equal(new[] { "1080p" }, v.Tags);
        Assert.Equal("https://www.xnxx.com/video-e0x1/56831/0/sample_clip_1", v.SourcePageUrl);
        Assert.Equal("/search/abc/1", listing.NextPath);
    }

    [Fact]
    public void Last_page_has_no_next_and_best_layout_uses_current_marker()
    {
        Assert.Null(XVideosParser.ParseListing(XnFixtures.Listing(2, 3, morePages: false), "xnxx", false, XvSite.Xnxx).NextPath);
        var best = XVideosParser.ParseListing(XnFixtures.BestLayout, "xnxx", false, XvSite.Xnxx);
        var b = Assert.Single(best.Videos);
        Assert.Equal("bb1/best_clip", b.Id);
        Assert.Equal(TimeSpan.FromMinutes(13), b.Duration);
        Assert.Equal(new[] { "1080p" }, b.Tags);
        Assert.Equal("/best/2026-09/1", best.NextPath);
    }

    [Fact]
    public void Xvideos_profile_ignores_xnxx_links()
    {
        var ex = Assert.Throws<ProviderException>(() => XVideosParser.ParseListing(XnFixtures.Listing(2, 0, true).Replace("mozaique", "grid", StringComparison.Ordinal), "xvideos", false));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
    }

    [Fact]
    public void Categories_only_listing_paths_deduplicated()
    {
        var cats = XnxxProvider.ParseCategories(XnFixtures.Home);
        Assert.Equal(new[] { ("/todays-selection", "Today's selection"), ("/search/alpha?id=123", "Alpha") }, cats.Select(c => (c.Id, c.Name)));
    }

    [Fact]
    public async Task Sorts_categories_and_search_urls()
    {
        var (p, site) = Create();
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Popular }, CancellationToken.None);
        var featured = await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Featured }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest { CategoryId = "/search/alpha?id=123" }, CancellationToken.None);
        await p.SearchAsync(new SearchRequest("two words"), CancellationToken.None);
        Assert.Equal(new[] { "/best/2026-09", "/todays-selection", "/search/alpha?id=123", "/search/two%20words" }, site.Requests.Select(u => u.PathAndQuery));
        Assert.Equal("/todays-selection/1", featured.NextPageToken);
    }

    [Theory]
    [InlineData("/account/settings")]
    [InlineData("https://evil.com/search/x")]
    [InlineData("//evil.com/search/x")]
    [InlineData("/video-abc/1/0/x")]
    public async Task Category_ids_and_tokens_must_be_listing_paths(string value)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { CategoryId = value }, CancellationToken.None));
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { PageToken = value }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public async Task Resolve_hls_first_then_mp4()
    {
        var (p, _) = Create();
        var r = await p.ResolvePlaybackAsync("e0x0/56830/0/sample_clip_0", CancellationToken.None);
        Assert.Equal(new[] { StreamProtocol.Hls, StreamProtocol.Progressive, StreamProtocol.Progressive }, r.Variants.Select(v => v.Protocol));
        Assert.Equal(720, r.Variants[0].Height);
        Assert.Equal(TimeSpan.FromSeconds(609), r.Duration);
    }
}

[Trait("Category", "Live")]
public class XnxxLiveTests
{
    [Fact]
    public async Task Live_end_to_end()
    {
        if (Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") != "1")
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1) });
        var p = new XnxxProvider(http);
        var ct = CancellationToken.None;

        var cats = await p.GetCategoriesAsync(ct);
        Assert.True(cats.Count >= 20, $"categories: {cats.Count}");

        foreach (var request in new[]
                 {
                     new BrowseRequest { Sort = TubeSorts.Popular },
                     new BrowseRequest { Sort = TubeSorts.Featured },
                     new BrowseRequest { CategoryId = cats[2].Id },
                 })
        {
            var page = await p.BrowseAsync(request, ct);
            Assert.True(page.Items.Count >= 10, $"{request.Sort ?? request.CategoryId}: {page.Items.Count}");
            Assert.True(page.HasMore, $"{request.Sort ?? request.CategoryId}: no next page");
            Assert.All(page.Items, v => Assert.NotNull(v.Duration));
            var next = await p.BrowseAsync(request with { PageToken = page.NextPageToken }, ct);
            Assert.NotEqual(page.Items[0].Id, next.Items[0].Id);
        }

        var search = await p.SearchAsync(new SearchRequest("amateur"), ct);
        Assert.NotEmpty(search.Items);
        Assert.True(search.HasMore);

        var r = await p.ResolvePlaybackAsync(search.Items[0].Id, ct);
        Assert.Contains(r.Variants, v => v.Protocol == StreamProtocol.Hls);
        Assert.All(r.Variants, v => Assert.EndsWith("xnxx-cdn.com", v.Url.Host, StringComparison.Ordinal));
    }
}
