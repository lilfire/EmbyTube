using System.Net;
using EmbyTube.Application;
using EmbyTube.Application.Navigation;
using EmbyTube.Application.Playback;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Ids;
using EmbyTube.Core.Security;
using EmbyTube.Providers.Mock;

namespace EmbyTube.Core.Tests;

public class ChannelNavigatorTests
{
    private static ChannelNavigator Navigator(EmbyTubeSettings? settings = null, params ITubeProvider[] providers)
    {
        if (providers.Length == 0)
            providers = new ITubeProvider[] { new MockProvider() };
        var registry = new ProviderRegistry(providers, _ => true);
        var s = settings ?? EmbyTubeSettings.Default;
        return new ChannelNavigator(registry, () => s, NullLog.Instance);
    }

    [Fact]
    public async Task Root_lists_enabled_providers()
    {
        var items = await Navigator().GetItemsAsync(null, CancellationToken.None);
        var folder = Assert.IsType<NavFolder>(Assert.Single(items));
        Assert.Equal("EmbyTube Test Source", folder.Name);
    }

    [Fact]
    public async Task Root_with_no_providers_shows_message()
    {
        var registry = new ProviderRegistry(new[] { new MockProvider() }, _ => false);
        var nav = new ChannelNavigator(registry, () => EmbyTubeSettings.Default, NullLog.Instance);
        var items = await nav.GetItemsAsync(null, CancellationToken.None);
        Assert.IsType<NavMessage>(Assert.Single(items));
    }

    [Fact]
    public async Task Provider_menu_follows_capabilities_and_saved_searches()
    {
        var nav = Navigator(new EmbyTubeSettings { SavedSearches = new[] { "Sintel", "sintel", " " } });
        var menu = await nav.GetItemsAsync(ProviderFolder(), CancellationToken.None);
        Assert.Equal(new[] { Labels.Popular, Labels.Newest, Labels.Categories, Labels.SearchPrefix + "Sintel" }, menu.Select(i => i.Name));
    }

    [Fact]
    public async Task Paging_is_bounded_by_MaxPages()
    {
        var nav = Navigator(new EmbyTubeSettings { PageSize = 10, MaxPages = 3 });
        var folderId = ItemIds.Encode(new FolderId(FolderKind.Browse, MockProvider.ProviderId) { Sort = TubeSorts.Popular });
        var pages = 0;
        var seen = new HashSet<string>();
        while (folderId is not null)
        {
            var items = await nav.GetItemsAsync(folderId, CancellationToken.None);
            pages++;
            foreach (var v in items.OfType<NavVideo>())
                Assert.True(seen.Add(v.Id), "duplicate video across pages");
            folderId = items.OfType<NavFolder>().SingleOrDefault()?.Id;
        }

        Assert.Equal(3, pages);
        Assert.Equal(30, seen.Count);
    }

    [Fact]
    public async Task Categories_are_capped_and_category_listings_use_their_own_depth()
    {
        var nav = Navigator(new EmbyTubeSettings { MaxCategories = 2, CategoryMaxPages = 1, MaxPages = 3, PageSize = 10 });
        var cats = await nav.GetItemsAsync(ItemIds.Encode(new FolderId(FolderKind.Categories, "mock")), CancellationToken.None);
        Assert.Equal(2, cats.Count);

        var catListing = await nav.GetItemsAsync(cats[0].Id, CancellationToken.None);
        Assert.Empty(catListing.OfType<NavFolder>()); // no "Next page": CategoryMaxPages = 1

        var popular = await nav.GetItemsAsync(ItemIds.Encode(new FolderId(FolderKind.Browse, "mock") { Sort = TubeSorts.Popular }), CancellationToken.None);
        Assert.Single(popular.OfType<NavFolder>()); // main listings still page

        var all = Navigator(new EmbyTubeSettings { MaxCategories = 0 });
        Assert.Equal(4, (await all.GetItemsAsync(ItemIds.Encode(new FolderId(FolderKind.Categories, "mock")), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Failing_provider_yields_message_and_does_not_affect_others()
    {
        var nav = Navigator(null, new MockProvider(), new ThrowingProvider());
        var root = await nav.GetItemsAsync(null, CancellationToken.None);
        Assert.Equal(2, root.Count);

        var broken = ItemIds.Encode(new FolderId(FolderKind.Browse, "broken"));
        var items = await nav.GetItemsAsync(broken, CancellationToken.None);
        var msg = Assert.IsType<NavMessage>(Assert.Single(items));
        Assert.Equal(Labels.ForError(ProviderErrorKind.ParseChanged), msg.Name);

        var bug = ItemIds.Encode(new FolderId(FolderKind.Categories, "broken"));
        msg = Assert.IsType<NavMessage>(Assert.Single(await nav.GetItemsAsync(bug, CancellationToken.None)));
        Assert.Equal(Labels.ForError(ProviderErrorKind.Unknown), msg.Name);

        var ok = await nav.GetItemsAsync(ItemIds.Encode(new FolderId(FolderKind.Browse, "mock")), CancellationToken.None);
        Assert.NotEmpty(ok.OfType<NavVideo>());
    }

    [Fact]
    public async Task Unknown_or_disabled_folders_return_message()
    {
        var nav = Navigator();
        Assert.IsType<NavMessage>(Assert.Single(await nav.GetItemsAsync("garbage", CancellationToken.None)));
        Assert.IsType<NavMessage>(Assert.Single(await nav.GetItemsAsync(
            ItemIds.Encode(new FolderId(FolderKind.Browse, "nope")), CancellationToken.None)));
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Navigator().GetItemsAsync(ItemIds.Encode(new FolderId(FolderKind.Browse, "mock")), cts.Token));
    }

    private static string ProviderFolder() => ItemIds.Encode(new FolderId(FolderKind.Provider, MockProvider.ProviderId));

    private sealed class ThrowingProvider : ITubeProvider
    {
        public string Id => "broken";
        public string DisplayName => "Broken";
        public ProviderCapabilities Capabilities { get; } = new() { Browse = true, Categories = true };
        public IReadOnlyCollection<string> AllowedHosts { get; } = Array.Empty<string>();
        public Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct) => throw new NullReferenceException();
        public Task<Page<TubeVideo>> BrowseAsync(BrowseRequest request, CancellationToken ct) =>
            throw new ProviderException(ProviderErrorKind.ParseChanged, "layout changed");
        public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct) => throw new NotSupportedException();
    }
}

public class PlaybackServiceTests
{
    private static readonly UrlGuard PublicDns = new((_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") }));

    private static PlaybackService Service(ITubeProvider provider, EmbyTubeSettings? settings = null, UrlGuard? guard = null)
    {
        var s = settings ?? EmbyTubeSettings.Default;
        return new PlaybackService(new ProviderRegistry(new[] { provider }, _ => true), () => s, guard ?? PublicDns, NullLog.Instance);
    }

    [Fact]
    public async Task Resolves_fresh_on_every_call()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000);
        var mock = new MockProvider(now: () => now);
        var svc = Service(mock);
        var id = ItemIds.Encode(new VideoId("mock", "mp4-000"));

        var a = await svc.ResolveAsync(id, CancellationToken.None);
        now = now.AddMinutes(30);
        var b = await svc.ResolveAsync(id, CancellationToken.None);

        Assert.Equal(2, mock.ResolveCount);
        Assert.NotEqual(a.Variants[0].Url, b.Variants[0].Url);
    }

    [Fact]
    public async Task Private_address_variant_is_dropped()
    {
        var svc = Service(new MockProvider(), guard: new UrlGuard((_, _) => Task.FromResult(new[] { IPAddress.Loopback })));
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            svc.ResolveAsync(ItemIds.Encode(new VideoId("mock", "errors-001")), CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Blocked, ex.Kind);
    }

    [Fact]
    public async Task Empty_variants_is_unsupported()
    {
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            Service(new MockProvider()).ResolveAsync(ItemIds.Encode(new VideoId("mock", "errors-002")), CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Unsupported, ex.Kind);
    }

    [Fact]
    public async Task Transient_failure_is_retried_exactly_once()
    {
        var mock = new MockProvider();
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            Service(mock).ResolveAsync(ItemIds.Encode(new VideoId("mock", "errors-000")), CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Unavailable, ex.Kind);
        Assert.Equal(2, mock.ResolveCount);
    }

    [Fact]
    public async Task Non_video_ids_are_rejected()
    {
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            Service(new MockProvider()).ResolveAsync("f1|b|mock|||||1", CancellationToken.None));
        Assert.Equal(ProviderErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public void Ordering_respects_max_height_then_protocol_preference()
    {
        PlaybackVariant V(int h, StreamProtocol p) => new(new Uri($"https://example.com/{h}{p}"), p, p == StreamProtocol.Hls ? "hls" : "mp4") { Height = h };
        var variants = new[] { V(2160, StreamProtocol.Progressive), V(720, StreamProtocol.Progressive), V(1080, StreamProtocol.Hls), V(1080, StreamProtocol.Progressive), V(480, StreamProtocol.Hls) };

        var mp4First = PlaybackService.Order(variants, new EmbyTubeSettings { MaxHeight = 1080 });
        Assert.Equal(new[] { "1080Progressive", "1080Hls", "720Progressive", "480Hls", "2160Progressive" }, mp4First.Select(v => v.Url.AbsolutePath.Trim('/')));

        var hlsFirst = PlaybackService.Order(variants, new EmbyTubeSettings { MaxHeight = 1080, PreferHls = true });
        Assert.Equal("1080Hls", hlsFirst[0].Url.AbsolutePath.Trim('/'));

        var unlimited = PlaybackService.Order(variants, new EmbyTubeSettings { MaxHeight = 0 });
        Assert.Equal("2160Progressive", unlimited[0].Url.AbsolutePath.Trim('/'));

        // Ties (unknown height, same protocol) keep the provider's order.
        var high = new PlaybackVariant(new Uri("https://example.com/high"), StreamProtocol.Progressive, "mp4");
        var low = new PlaybackVariant(new Uri("https://example.com/low"), StreamProtocol.Progressive, "mp4");
        Assert.Equal(new[] { high, low }, PlaybackService.Order(new[] { high, low }, EmbyTubeSettings.Default));
    }
}

public class TtlCacheTests
{
    [Fact]
    public void Expires_and_bounds_size()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new TtlCache<int, string>(10, () => now);
        cache.Set(1, "a", TimeSpan.FromMinutes(1));
        Assert.True(cache.TryGet(1, out var v));
        Assert.Equal("a", v);

        now = now.AddMinutes(2);
        Assert.False(cache.TryGet(1, out _));

        for (var i = 0; i < 100; i++)
            cache.Set(i, "x", TimeSpan.FromMinutes(i + 1));
        Assert.True(cache.Count <= 10);

        cache.Set(500, "zero", TimeSpan.Zero);
        Assert.False(cache.TryGet(500, out _));
    }
}
