using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.Pornhat;

namespace EmbyTube.Provider.Tests;

/// <summary>Synthetic Pornhat-network fixtures (2026-10-11 markup), neutral content, fake URLs.</summary>
internal static class PhatFixtures
{
    public static string Listing(int count, string? next, string prefix = "clip") =>
        "<div class=\"main\"><div class=\"list_video_wrapper\">"
        + string.Concat(Enumerable.Range(0, count).Select(i =>
            "<div class=\"item thumb-bl thumb-video items-videos \"><div class=\"thumb\">"
            + $"<a href=\"/video/{prefix}-{i}/\" title=\"Sample {prefix} {i} &amp; co\" data-preview-custom=\"https://www.pornhat.com/p.mp4\">"
            + $"<img class=\"thumb lazy-load\" src=\"data:image/gif;base64,R0lG\" data-original=\"https://static.pornhat.com/t/{i}.jpg\" alt=\"x\"/>"
            + $"<span class=\"duration_item\"> 1{i}:05 </span></a></div>"
            + $"<div class=\"thumb-title\"><a href=\"/video/{prefix}-{i}/\" title=\"Sample {prefix} {i} &amp; co\">Sample</a></div></div>"))
        + "</div>"
        + (next is null ? string.Empty : $"<a class=\"pagination-next\" href=\"{next}\">Next</a>")
        + "</div>";

    public const string Channels =
        "<div class=\"main\"><div class=\"thumb-bl\"><div class=\"thumb thumb-ctr\"><a title=\"Studio A\" href=\"/sites/studio-a/\"><img src=\"https://static.pornhat.com/c.jpg\"/></a></div></div>"
        + "<div class=\"thumb-bl\"><div class=\"thumb thumb-ctr\"><a title=\"Studio &amp; B\" href=\"/sites/studio-b/\"></a></div></div>"
        + "<div class=\"thumb-bl\"><a title=\"Studio A\" href=\"/sites/studio-a/\"></a></div>"
        + "<li><a href=\"/channels/updated/\" title=\"Recently updated\">Updated</a></li>"
        + "<div class=\"thumb-bl\"><a title=\"Model C\" href=\"/channels/model-c/\"></a></div></div>";

    public const string VideoPage =
        "<meta property=\"og:title\" content=\"Sample video\" /><video><source src=\"https://www.pornhat.com/get_file/1/a/auto.m3u8\" title=\"Auto\" label=\"Auto\"/>"
        + "<source src=\"https://www.pornhat.com/get_file/1/a/360.m3u8\" title=\"360p\" label=\"360p\"/>"
        + "<source src=\"https://www.pornhat.com/get_file/1/a/720.m3u8\" title=\"720p\" label=\"720p\"/>"
        + "<source src=\"https://www.pornhat.com/get_file/1/a/evil.m3u8\" title=\"1080p\" label=\"1080p\"/></video>";

    public static FakeSite Site() => new(u =>
    {
        var pq = u.PathAndQuery;
        if (u.Host == "cdn.privatehost.com")
            return (HttpStatusCode.OK, "#EXTM3U");
        if (pq.StartsWith("/get_file/", StringComparison.Ordinal))
        {
            // Site redirector → CDN; the 1080p one points off-allowlist and must be dropped.
            var target = pq.Contains("evil", StringComparison.Ordinal) ? "https://evil.example/x.m3u8" : "https://cdn.privatehost.com/hls" + pq;
            return (HttpStatusCode.Redirect, target);
        }

        if (pq == "/")
            return (HttpStatusCode.OK, Listing(5, "/2/"));
        if (pq == "/2/")
            return (HttpStatusCode.OK, Listing(3, null, "page2"));
        if (pq == "/channels/")
            return (HttpStatusCode.OK, Channels);
        if (pq.StartsWith("/sites/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing(4, pq + "2/", "chan"));
        if (pq.StartsWith("/search/zzzz-no-such-video-zzzz", StringComparison.Ordinal))
            return (HttpStatusCode.NotFound, "none");
        if (pq.StartsWith("/search/", StringComparison.Ordinal))
            return (HttpStatusCode.OK, Listing(2, null, "found"));
        if (pq == "/video/clip-0/")
            return (HttpStatusCode.OK, VideoPage);
        if (pq == "/video/nostream/")
            return (HttpStatusCode.OK, "<meta property=\"og:title\" content=\"x\" />");
        return (HttpStatusCode.NotFound, "not found");
    });
}

/// <summary>FakeSite variant that turns a "302 + target" tuple into a real redirect response.</summary>
internal sealed class RedirectingSite : HttpMessageHandler
{
    private readonly FakeSite _inner;

    public RedirectingSite(FakeSite inner) => _inner = inner;

    public List<Uri> Requests => _inner.Requests;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await new HttpMessageInvoker(_inner).SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            var target = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Headers.Location = new Uri(target);
        }

        return response;
    }
}

public class PornhatContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() => new PornhatNetworkProvider(
        PornhatSite.Pornhat,
        new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, new RedirectingSite(PhatFixtures.Site())));

    protected override string PlayableVideoId => "clip-0";
}

public class PornhatTests
{
    private static (PornhatNetworkProvider P, RedirectingSite Site) Create()
    {
        var site = new RedirectingSite(PhatFixtures.Site());
        return (new PornhatNetworkProvider(PornhatSite.Pornhat, new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, site)), site);
    }

    [Fact]
    public void Parses_listing_deduplicating_repeated_links()
    {
        var (p, _) = Create();
        var videos = p.ParseListing(PhatFixtures.Listing(3, "/2/"), out var next);
        Assert.Equal(new[] { "clip-0", "clip-1", "clip-2" }, videos.Select(v => v.Id));
        Assert.Equal("Sample clip 1 & co", videos[1].Title);
        Assert.Equal(TimeSpan.FromSeconds((11 * 60) + 5), videos[1].Duration);
        Assert.Equal("https://static.pornhat.com/t/1.jpg", videos[1].ThumbnailUrl);
        Assert.Equal("/2/", next);
    }

    [Fact]
    public void Parses_channels_as_categories()
    {
        var cats = PornhatNetworkProvider.ParseChannels(PhatFixtures.Channels);
        Assert.Equal(new[] { ("sites/studio-a", "Studio A"), ("sites/studio-b", "Studio & B"), ("channels/model-c", "Model C") }, cats.Select(c => (c.Id, c.Name)));
    }

    [Fact]
    public async Task Urls_for_browse_channel_and_search()
    {
        var (p, site) = Create();
        await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, CancellationToken.None);
        var chan = await p.BrowseAsync(new BrowseRequest { CategoryId = "sites/studio-a" }, CancellationToken.None);
        await p.SearchAsync(new SearchRequest("two  words"), CancellationToken.None);
        Assert.Equal(new[] { "/", "/sites/studio-a/", "/search/two-words/" }, site.Requests.Select(u => u.PathAndQuery));
        Assert.Equal("/sites/studio-a/2/", chan.NextPageToken);
    }

    [Fact]
    public async Task Resolve_follows_redirects_to_cdn_and_drops_off_allowlist_quality()
    {
        var (p, _) = Create();
        var r = await p.ResolvePlaybackAsync("clip-0", CancellationToken.None);
        Assert.Equal(new int?[] { 720, 360 }, r.Variants.Select(v => v.Height));
        Assert.All(r.Variants, v =>
        {
            Assert.Equal("cdn.privatehost.com", v.Url.Host);
            Assert.Equal(StreamProtocol.Hls, v.Protocol);
        });
    }

    [Fact]
    public async Task No_sources_is_access_denied()
    {
        var (p, _) = Create();
        var ex = await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("nostream", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.AccessDenied, ex.Kind);
    }

    [Theory]
    [InlineData("/video/clip-0/")]
    [InlineData("/player/html.php")]
    [InlineData("/?action=x")]
    [InlineData("https://evil.com/2/")]
    public async Task Tokens_must_be_listing_paths(string token)
    {
        var (p, site) = Create();
        await Assert.ThrowsAsync<ProviderException>(() => p.BrowseAsync(new BrowseRequest { PageToken = token }, CancellationToken.None));
        Assert.Empty(site.Requests);
    }

    [Fact]
    public void Each_site_allows_its_own_domain_and_the_cdn()
    {
        var p = new PornhatNetworkProvider(PornhatSite.PerfectGirls, new SafeHttpClient());
        Assert.Equal(new[] { "perfectgirls.xxx", "privatehost.com" }, p.AllowedHosts);
        Assert.Equal(4, PornhatSite.All.Select(s => s.Id).Distinct().Count());
    }
}

[Trait("Category", "Live")]
public class PornhatLiveTests
{
    public static TheoryData<string> Sites => new() { "pornhat", "okxxx", "perfectgirls", "okporn" };

    [Theory]
    [MemberData(nameof(Sites))]
    public async Task Live_end_to_end(string siteId)
    {
        if (Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") != "1")
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1) });
        var p = new PornhatNetworkProvider(PornhatSite.All.Single(s => s.Id == siteId), http);
        var ct = CancellationToken.None;

        var cats = await p.GetCategoriesAsync(ct);
        Assert.True(cats.Count >= 10, $"{siteId} channels: {cats.Count}");

        var newest = await p.BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest }, ct);
        Assert.True(newest.Items.Count >= 20, $"{siteId} newest: {newest.Items.Count}");
        Assert.True(newest.HasMore, $"{siteId}: no next page");
        Assert.True(newest.Items.Count(v => v.Duration is not null) >= newest.Items.Count / 2, $"{siteId}: durations missing");
        var page2 = await p.BrowseAsync(new BrowseRequest { PageToken = newest.NextPageToken }, ct);
        Assert.NotEqual(newest.Items[0].Id, page2.Items[0].Id);

        // Some channels are empty (seen on perfectgirls); at least one of the first five must list videos.
        var anyChannelVideos = false;
        foreach (var cat in cats.Take(5))
        {
            if ((await p.BrowseAsync(new BrowseRequest { CategoryId = cat.Id }, ct)).Items.Count > 0)
            {
                anyChannelVideos = true;
                break;
            }
        }

        Assert.True(anyChannelVideos, $"{siteId}: first five channels are empty");

        var search = await p.SearchAsync(new SearchRequest("blonde"), ct);
        Assert.NotEmpty(search.Items);

        var r = await p.ResolvePlaybackAsync(newest.Items[0].Id, ct);
        Assert.NotEmpty(r.Variants);
        Assert.All(r.Variants, v => Assert.EndsWith("privatehost.com", v.Url.Host, StringComparison.Ordinal));
    }
}
