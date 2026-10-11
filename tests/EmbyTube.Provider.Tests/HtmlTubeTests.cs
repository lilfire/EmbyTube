using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.Html;
using static EmbyTube.Providers.Html.HtmlTubeSite;

namespace EmbyTube.Provider.Tests;

/// <summary>Engine tests with a synthetic site profile and neutral fixtures.</summary>
internal static class HtFixtures
{
    public static readonly HtmlTubeSite Site = new()
    {
        Id = "synthetic",
        DisplayName = "Synthetic Tube",
        BaseUrl = "https://www.example.com/",
        AllowedHosts = new[] { "example.com", "cdn.example.net" },
        Sorts = new Dictionary<string, string> { [TubeSorts.Newest] = "/new/1", [TubeSorts.Popular] = "/top/1" },
        ItemStart = R(@"<div class=""tile"""),
        ItemLink = R(@"href=""(/watch/\d+/[a-z0-9\-]+)"""),
        ItemTitle = R(@"\balt=""([^""]+)"""),
        ItemThumb = R(@"\bdata-src=""([^""]+)"""),
        ItemDuration = R(@"class=""dur"">([\d:]+)<"),
        NextPage = R(@"<link rel=""next"" href=""([^""]+)"""),
        VideoPath = R(@"^watch/\d+/[a-z0-9\-]+$"),
        ListingPath = R(@"^/(?:new|top|cat/[a-z]+)/\d+$|^/search\?q=[^&]+(?:&p=\d+)?$"),
        CategoriesPage = "/cats",
        CategoryLink = R(@"<a class=""cat"" href=""(/cat/[a-z]+/1)"">([^<]+)</a>"),
        CategoryPath = R(@"^cat/[a-z]+/1$"),
        SearchTemplate = "/search?q={q}",
        SkipItem = R(@"class=""premium"""),
    };

    public static string Listing(int count, string? next, string prefix = "c") =>
        string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<div class=\"tile\"><a href=\"/watch/{100 + i}/{prefix}-{i}\"><img src=\"data:x\" data-src=\"https://img.example.com/{i}.jpg\" alt=\"Clip {i} &amp; co\"></a><span class=\"dur\">1:0{i}</span></div>"))
        + "<div class=\"tile\"><a href=\"/watch/999/paid\"><img alt=\"Paid\"></a><span class=\"premium\"></span></div>"
        + "<div class=\"tile\"><a href=\"https://evil.com/watch/1/x\"><img alt=\"Offsite\"></a></div>"
        + (next is null ? string.Empty : $"<link rel=\"next\" href=\"{next}\">");

    public static FakeSite Fake() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (u.Host == "cdn.example.net")
            return (HttpStatusCode.OK, "ok");
        if (u.Host == "redirect.evil.com")
            return (HttpStatusCode.OK, "evil");
        if (pq == "/new/1" || pq == "/top/1" || pq.StartsWith("/cat/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing(3, "https://www.example.com/new/2"));
        if (pq == "/new/2")
            return (HttpStatusCode.OK, Listing(2, null, "p2"));
        if (pq == "/cats")
            return (HttpStatusCode.OK, "<a class=\"cat\" href=\"/cat/alpha/1\">Alpha</a><a class=\"cat\" href=\"/cat/beta/1\">Beta &amp; Co</a><a class=\"cat\" href=\"/evil/1\">Evil</a>");
        if (pq.StartsWith("/search?q=zzzz", StringComparison.Ordinal))
            return (HttpStatusCode.OK, "<p>No results</p>");
        if (pq.StartsWith("/search?q=", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing(2, null, "s"));
        if (pq == "/watch/100/c-0")
            return (HttpStatusCode.OK,
                "<meta property=\"og:title\" content=\"Clip 0\"><video>"
                + "<source src=\"https://cdn.example.net/v/1080.mp4\" label=\"1080p\" type=\"video/mp4\">"
                + "<source src=\"https://cdn.example.net/v/master.m3u8\" title=\"720p\">"
                + "<source src=\"https://cdn.example.net/v/auto.m3u8\" label=\"Auto\">"
                + "<source src=\"https://redirect.evil.com/x.mp4\" label=\"480p\">"
                + "<source src=\"http://cdn.example.net/insecure.mp4\" label=\"360p\"></video>");
        return (HttpStatusCode.NotFound, "");
    });
}

public class HtmlTubeContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() =>
        new HtmlTubeProvider(HtFixtures.Site, new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, HtFixtures.Fake()));

    protected override string PlayableVideoId => "watch/100/c-0";
}

public class HtmlTubeTests
{
    private static (HtmlTubeProvider P, FakeSite Site) Create(HtmlTubeSite? site = null)
    {
        var fake = HtFixtures.Fake();
        return (new HtmlTubeProvider(site ?? HtFixtures.Site, new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, fake)), fake);
    }

    [Fact]
    public void Listing_skips_premium_and_offsite_items_and_normalizes_next()
    {
        var (p, _) = Create();
        var videos = p.ParseListing(HtFixtures.Listing(3, "https://www.example.com/new/2"), out var next);
        Assert.Equal(new[] { "watch/100/c-0", "watch/101/c-1", "watch/102/c-2" }, videos.Select(v => v.Id));
        Assert.Equal("Clip 1 & co", videos[1].Title);
        Assert.Equal(TimeSpan.FromSeconds(61), videos[1].Duration);
        Assert.Equal("https://img.example.com/1.jpg", videos[1].ThumbnailUrl);
        Assert.Equal("/new/2", next);
    }

    [Fact]
    public void Unrecognized_tiles_are_ParseChanged()
    {
        var (p, _) = Create();
        var ex = Assert.Throws<ProviderException>(() => p.ParseListing("<div class=\"tile\"><a href=\"/other\">x</a></div>", out _));
        Assert.Equal(ProviderErrorKind.ParseChanged, ex.Kind);
    }

    [Fact]
    public async Task Sorts_categories_and_search()
    {
        var (p, site) = Create();
        Assert.Equal(new[] { TubeSorts.Newest, TubeSorts.Popular }, p.Capabilities.Sorts);
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Popular }, CancellationToken.None);
        await p.BrowseAsync(new BrowseRequest(), CancellationToken.None);
        var cats = await p.GetCategoriesAsync(CancellationToken.None);
        Assert.Equal(new[] { ("cat/alpha/1", "Alpha"), ("cat/beta/1", "Beta & Co") }, cats.Select(c => (c.Id, c.Name)));
        await p.BrowseAsync(new BrowseRequest { CategoryId = "cat/alpha/1" }, CancellationToken.None);
        await p.SearchAsync(new SearchRequest("two words"), CancellationToken.None);
        Assert.Equal(new[] { "/top/1", "/new/1", "/cats", "/cat/alpha/1", "/search?q=two+words" }, site.Requests.Select(u => u.PathAndQuery));
    }

    [Fact]
    public async Task Resolve_keeps_only_https_allowlisted_sources_best_first()
    {
        var (p, _) = Create();
        var r = await p.ResolvePlaybackAsync("watch/100/c-0", CancellationToken.None);
        Assert.Equal(new[] { ("mp4", 1080), ("hls", 720) }, r.Variants.Select(v => (v.Container, v.Height ?? 0)));
        Assert.All(r.Variants, v => Assert.Equal("cdn.example.net", v.Url.Host));
    }

    [Theory]
    [InlineData("/watch/100/c-0")]
    [InlineData("https://evil.com/new/2")]
    [InlineData("/admin")]
    public async Task Tokens_must_be_listing_paths(string token)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { PageToken = token }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public async Task Search_disabled_profile_reports_capability_and_throws()
    {
        var (p, _) = Create(HtFixtures.Site with { SearchTemplate = null });
        Assert.False(p.Capabilities.Search);
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.SearchAsync(new SearchRequest("x"), CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Unsupported, ex.Kind);
    }

    [Theory]
    [InlineData("720p", 720)]
    [InlineData("4", 2160)]
    [InlineData("4K", 2160)]
    [InlineData("1080", 1080)]
    public void Source_labels_map_to_heights(string label, int expected)
    {
        var s = Assert.Single(HtmlTubeProvider.ParseSourceTags($"<source src=\"https://a.example/v.mp4\" size=\"{label}\">"));
        Assert.Equal(expected, s.Height);
    }

    [Fact]
    public void Kvs_uses_plain_urls_only_with_labels()
    {
        const string html = "var flashvars = { video_url: 'https://www.example.com/get_file/1/a.mp4/', video_url_text: '480p',"
            + " video_alt_url: 'function/0/https://www.example.com/get_file/1/obf.mp4/', video_alt_url_text: '720p',"
            + " video_alt_url2: 'https://www.example.com/get_file/1/b.mp4/', video_alt_url2_text: '1080p HD' };";
        var list = HtmlTubeProvider.ParseKvs(html);
        Assert.Equal(new[] { ("a.mp4", 480), ("b.mp4", 1080) }, list.Select(x => (x.Url.Segments[^1].TrimEnd('/'), x.Height ?? 0)));
    }

    [Fact]
    public void Content_url_is_used_only_for_media_files()
    {
        const string html = "<meta itemprop=\"contentUrl\" content=\"https://stream.example.com/v.mp4?token=x\">"
            + "<script>{\"contentUrl\": \"https:\\/\\/www.example.com\\/logo.png\"}</script>";
        var list = HtmlTubeProvider.ParseSourceTags(html, includeContentUrl: true);
        Assert.Equal("stream.example.com", Assert.Single(list).Url.Host);
        Assert.Empty(HtmlTubeProvider.ParseSourceTags(html));
    }

    [Theory]
    [InlineData("15m:00s", 900)]
    [InlineData("1h:02m:03s", 3723)]
    [InlineData("28 min", 1680)]
    [InlineData("12:34", 754)]
    public void Durations_in_site_formats(string text, int seconds)
    {
        var site = HtFixtures.Site with { ItemDuration = R(@"class=""dur"">([^<]+)<") };
        var (p, _) = Create(site);
        var html = "<div class=\"tile\"><a href=\"/watch/1/x\"><img alt=\"X\"></a><span class=\"dur\">" + text + "</span></div>";
        Assert.Equal(TimeSpan.FromSeconds(seconds), Assert.Single(p.ParseListing(html, out _)).Duration);
    }

    [Fact]
    public async Task Referer_sites_attach_headers_to_variants()
    {
        var (p, _) = Create(HtFixtures.Site with { StreamNeedsReferer = true });
        var r = await p.ResolvePlaybackAsync("watch/100/c-0", CancellationToken.None);
        Assert.All(r.Variants, v =>
        {
            Assert.Equal("https://www.example.com/", v.Headers["Referer"]);
            Assert.True(v.Headers.ContainsKey("User-Agent"));
        });
    }

    [Fact]
    public async Task Sub_page_strategy_follows_one_site_link()
    {
        var fake = new FakeSite(u => u.PathAndQuery switch
        {
            "/watch/1/x" => (HttpStatusCode.OK, "<iframe src='/inc/clic.php?video=AB1&amp;cat=x'></iframe>"),
            "/inc/clic.php?video=AB1&cat=x" => (HttpStatusCode.OK, "var f = ['https://cdn.example.net/v/720.mp4', \"https://evil.com/x.mp4\"];"),
            _ when u.Host == "cdn.example.net" => (HttpStatusCode.OK, "ok"),
            _ => (HttpStatusCode.NotFound, ""),
        });
        var site = HtFixtures.Site with { Streams = StreamStrategy.SubPage, SubPage = R(@"src='(/inc/clic\.php\?video=[A-Za-z0-9]+&(?:amp;)?cat=x)'"), VideoPath = R(@"^watch/\d+/[a-z]+$") };
        var p = new HtmlTubeProvider(site, new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, fake));
        var r = await p.ResolvePlaybackAsync("watch/1/x", CancellationToken.None);
        Assert.Equal("cdn.example.net", Assert.Single(r.Variants).Url.Host);
    }

    [Theory]
    [InlineData("/media/videos/a.mp4", "https://www.example.com/media/videos/a.mp4")]
    [InlineData("//cdn.example.net/a.mp4", "https://cdn.example.net/a.mp4")]
    [InlineData("b.mp4", "https://www.example.com/watch/1/b.mp4")]
    public void Relative_sources_resolve_against_the_page_on_every_os(string src, string expected)
    {
        var list = HtmlTubeProvider.ParseSourceTags($"<source src=\"{src}\" type=\"video/mp4\">", page: new Uri("https://www.example.com/watch/1/x"));
        Assert.Equal(expected, Assert.Single(list).Url.AbsoluteUri);
    }

    [Fact]
    public void Real_profiles_are_consistent()
    {
        Assert.Equal(HtmlTubeSites.All.Count, HtmlTubeSites.All.Select(s => s.Id).Distinct().Count());
        Assert.All(HtmlTubeSites.All, s =>
        {
            Assert.NotEmpty(s.Sorts);
            Assert.True(Core.Security.UrlGuard.IsHostAllowed(new Uri(s.BaseUrl).Host, s.AllowedHosts), s.Id);
            Assert.Matches("^[a-z0-9-]+$", s.Id);
        });
    }
}
