using EmbyTube.Core.Contracts;
using EmbyTube.Core.Security;
using EmbyTube.Providers.Mock;

namespace EmbyTube.Provider.Tests;

/// <summary>
/// Contract every provider must satisfy. Add a subclass per provider; live-site
/// providers must run these against offline fixtures (see docs/provider-authoring.md).
/// </summary>
public abstract class ProviderContractTests
{
    protected abstract ITubeProvider CreateProvider();

    /// <summary>A video id known to resolve to at least one variant.</summary>
    protected abstract string PlayableVideoId { get; }

    [Fact]
    public void Id_is_valid()
    {
        var p = CreateProvider();
        Assert.Matches("^[a-z0-9-]+$", p.Id);
        Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
        Assert.NotEmpty(p.AllowedHosts);
    }

    [Fact]
    public async Task Browse_returns_stable_unique_ids_with_metadata()
    {
        var p = CreateProvider();
        if (!p.Capabilities.Browse)
            return;

        var a = await p.BrowseAsync(new BrowseRequest { PageSize = 20 }, CancellationToken.None);
        var b = await p.BrowseAsync(new BrowseRequest { PageSize = 20 }, CancellationToken.None);

        Assert.NotEmpty(a.Items);
        Assert.Equal(a.Items.Select(v => v.Id), b.Items.Select(v => v.Id));
        Assert.Equal(a.Items.Count, a.Items.Select(v => v.Id).Distinct().Count());
        Assert.All(a.Items, v =>
        {
            Assert.Equal(p.Id, v.ProviderId);
            Assert.False(string.IsNullOrWhiteSpace(v.Title));
            if (v.ThumbnailUrl is not null)
                Assert.True(UrlGuard.IsAllowedSyntax(new Uri(v.ThumbnailUrl), p.AllowedHosts, out _), "thumbnail outside allowlist");
        });
    }

    [Fact]
    public async Task Pagination_advances_and_terminates()
    {
        var p = CreateProvider();
        if (!p.Capabilities.Pagination)
            return;

        var ids = new HashSet<string>();
        string? token = null;
        for (var i = 0; i < 50; i++)
        {
            var page = await p.BrowseAsync(new BrowseRequest { PageToken = token, PageSize = 25 }, CancellationToken.None);
            foreach (var v in page.Items)
                Assert.True(ids.Add(v.Id), $"duplicate id across pages: {v.Id}");
            if (!page.HasMore)
                return;
            token = page.NextPageToken;
        }

        Assert.Fail("pagination did not terminate within 50 pages");
    }

    [Fact]
    public async Task Invalid_page_token_is_a_typed_error()
    {
        var p = CreateProvider();
        if (!p.Capabilities.Pagination)
            return;
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            p.BrowseAsync(new BrowseRequest { PageToken = "!!not-a-token!!" }, CancellationToken.None));
        Assert.NotEqual(ProviderErrorKind.Unknown, ex.Kind);
    }

    [Fact]
    public async Task Search_with_no_hits_returns_empty_page()
    {
        var p = CreateProvider();
        if (!p.Capabilities.Search)
            return;
        var page = await p.SearchAsync(new SearchRequest("zzzz-no-such-video-zzzz"), CancellationToken.None);
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task Categories_have_unique_ids()
    {
        var p = CreateProvider();
        if (!p.Capabilities.Categories)
            return;
        var cats = await p.GetCategoriesAsync(CancellationToken.None);
        Assert.NotEmpty(cats);
        Assert.Equal(cats.Count, cats.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public async Task Resolve_returns_allowlisted_variants()
    {
        var p = CreateProvider();
        var result = await p.ResolvePlaybackAsync(PlayableVideoId, CancellationToken.None);
        Assert.NotEmpty(result.Variants);
        Assert.All(result.Variants, v =>
            Assert.True(UrlGuard.IsAllowedSyntax(v.Url, p.AllowedHosts, out var reason), reason));
    }

    [Fact]
    public async Task Unknown_video_is_typed_error_or_null()
    {
        var p = CreateProvider();
        Assert.Null(await p.GetVideoAsync("does-not-exist-000", CancellationToken.None));
        await Assert.ThrowsAsync<ProviderException>(() => p.ResolvePlaybackAsync("does-not-exist-000", CancellationToken.None));
    }

    [Fact]
    public async Task Honors_cancellation()
    {
        var p = CreateProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => p.BrowseAsync(new BrowseRequest(), cts.Token));
    }
}

public class MockProviderContractTests : ProviderContractTests
{
    protected override ITubeProvider CreateProvider() => new MockProvider();

    protected override string PlayableVideoId => "hls-001";
}

public class MockProviderTests
{
    [Fact]
    public async Task Multi_category_offers_several_variants()
    {
        var result = await new MockProvider().ResolvePlaybackAsync("multi-000", CancellationToken.None);
        Assert.True(result.Variants.Count >= 2);
        Assert.Contains(result.Variants, v => v.Protocol == StreamProtocol.Hls);
        Assert.Contains(result.Variants, v => v.Protocol == StreamProtocol.Progressive);
    }

    [Fact]
    public async Task Overrides_replace_streams_and_extend_allowlist()
    {
        var p = new MockProvider(new MockOptions { Mp4Url = new Uri("https://media.example.org/test.mp4") });
        Assert.Contains("media.example.org", p.AllowedHosts);
        var result = await p.ResolvePlaybackAsync("mp4-000", CancellationToken.None);
        Assert.Equal("media.example.org", result.Variants[0].Url.Host);
    }

    [Fact]
    public async Task Newest_sort_is_descending_by_date()
    {
        var page = await new MockProvider().BrowseAsync(new BrowseRequest { Sort = TubeSorts.Newest, PageSize = 50 }, CancellationToken.None);
        var dates = page.Items.Select(v => v.PublishedAt!.Value).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }
}
