using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;
using EmbyTube.Providers.Html;
using Xunit.Abstractions;

namespace EmbyTube.Provider.Tests;

/// <summary>Opt-in live checks for every <see cref="HtmlTubeSites"/> profile (EMBYTUBE_LIVE_TESTS=1).</summary>
[Trait("Category", "Live")]
public class HtmlTubeLiveTests
{
    private readonly ITestOutputHelper _out;

    public HtmlTubeLiveTests(ITestOutputHelper output) => _out = output;

    public static TheoryData<string> Sites
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var s in HtmlTubeSites.All)
                data.Add(s.Id);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Sites))]
    public async Task Live_end_to_end(string siteId)
    {
        if (Environment.GetEnvironmentVariable("EMBYTUBE_LIVE_TESTS") != "1")
            return;

        using var http = new SafeHttpClient(new SafeHttpOptions { MinIntervalPerHost = TimeSpan.FromSeconds(1.5) });
        var site = HtmlTubeSites.All.Single(s => s.Id == siteId);
        var p = new HtmlTubeProvider(site, http);
        var ct = CancellationToken.None;

        var first = await p.BrowseAsync(new BrowseRequest(), ct);
        _out.WriteLine($"{siteId} default listing: {first.Items.Count} items, next={first.NextPageToken}, durations={first.Items.Count(v => v.Duration is not null)}, thumbs={first.Items.Count(v => v.ThumbnailUrl is not null)}");
        Assert.True(first.Items.Count >= 10, $"{siteId}: only {first.Items.Count} items");
        Assert.True(first.HasMore, $"{siteId}: no next page");
        Assert.True(first.Items.Count(v => v.Duration is not null) >= first.Items.Count / 2, $"{siteId}: durations missing");
        Assert.True(first.Items.Count(v => v.ThumbnailUrl is not null) >= first.Items.Count / 2, $"{siteId}: thumbnails missing");

        var page2 = await p.BrowseAsync(new BrowseRequest { PageToken = first.NextPageToken }, ct);
        _out.WriteLine($"{siteId} page 2: {page2.Items.Count} items");
        Assert.NotEmpty(page2.Items);
        // Some sites repeat a sticky item at the top of every page; page 2 must still bring new videos.
        Assert.Contains(page2.Items, v => first.Items.All(f => f.Id != v.Id));

        if (p.Capabilities.Categories)
        {
            var cats = await p.GetCategoriesAsync(ct);
            _out.WriteLine($"{siteId} categories: {cats.Count} (first: {cats[0].Id} = {cats[0].Name})");
            Assert.True(cats.Count >= 5, $"{siteId}: {cats.Count} categories");
            var anyCategory = false;
            foreach (var cat in cats.Take(5))
            {
                var listing = await p.BrowseAsync(new BrowseRequest { CategoryId = cat.Id }, ct);
                _out.WriteLine($"{siteId} category {cat.Id}: {listing.Items.Count} items");
                if (listing.Items.Count > 0)
                {
                    anyCategory = true;
                    break;
                }
            }

            Assert.True(anyCategory, $"{siteId}: first categories are empty");
        }

        if (p.Capabilities.Search)
        {
            var search = await p.SearchAsync(new SearchRequest("blonde"), ct);
            _out.WriteLine($"{siteId} search: {search.Items.Count} items, next={search.NextPageToken}");
            Assert.NotEmpty(search.Items);
        }

        PlaybackResult? r = null;
        foreach (var v in first.Items.Take(3))
        {
            try
            {
                r = await p.ResolvePlaybackAsync(v.Id, ct);
                break;
            }
            catch (ProviderException ex)
            {
                _out.WriteLine($"{siteId} resolve {v.Id}: {ex.Kind} {ex.Message}");
            }
        }

        Assert.NotNull(r);
        _out.WriteLine($"{siteId} variants: {string.Join(", ", r!.Variants.Select(v => $"{v.Protocol}/{v.Height}p@{v.Url.Host}"))}");
        Assert.All(r.Variants, v => Assert.True(Core.Security.UrlGuard.IsAllowedSyntax(v.Url, p.AllowedHosts, out _), v.Url.Host));
    }
}
