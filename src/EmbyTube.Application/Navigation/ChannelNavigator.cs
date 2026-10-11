using EmbyTube.Core.Contracts;
using EmbyTube.Core.Ids;

namespace EmbyTube.Application.Navigation;

public abstract record NavItem(string Id, string Name);

public sealed record NavFolder(string Id, string Name) : NavItem(Id, Name)
{
    public string? ImageUrl { get; init; }
}

public sealed record NavVideo(string Id, TubeVideo Video) : NavItem(Id, Video.Title);

/// <summary>Informational entry (errors, hints). Rendered as an empty folder.</summary>
public sealed record NavMessage(string Id, string Name) : NavItem(Id, Name);

/// <summary>
/// Emby-independent channel navigation:
/// root → sources → Popular/Newest/Categories/saved searches → video list → "Next page".
/// Provider failures are contained to that provider's folder and turned into a
/// message entry; they never break other providers or the root.
/// </summary>
public sealed class ChannelNavigator
{
    private readonly ProviderRegistry _registry;
    private readonly Func<EmbyTubeSettings> _settings;
    private readonly IEmbyTubeLog _log;
    private readonly TtlCache<string, IReadOnlyList<NavItem>> _cache = new(maxEntries: 500);

    public ChannelNavigator(ProviderRegistry registry, Func<EmbyTubeSettings> settings, IEmbyTubeLog log)
    {
        _registry = registry;
        _settings = settings;
        _log = log;
    }

    public void InvalidateCache() => _cache.Clear();

    public async Task<IReadOnlyList<NavItem>> GetItemsAsync(string? folderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(folderId))
            return GetRoot();

        if (!ItemIds.TryDecodeFolder(folderId, out var folder) || folder is null)
            return new[] { Message(folderId, Labels.UnknownFolder) };

        if (folder.Kind == FolderKind.Info)
            return Array.Empty<NavItem>();

        var provider = _registry.GetEnabled(folder.ProviderId);
        if (provider is null)
            return new[] { Message(folderId, Labels.ProviderDisabled) };

        var settings = _settings();
        if (_cache.TryGet(folderId, out var cached))
            return cached;

        IReadOnlyList<NavItem> items;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(settings.ProviderTimeout);
            items = await LoadAsync(provider, folder, settings, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _log.Warn($"Provider '{provider.Id}' timed out listing {folder.Kind}.");
            return new[] { Message(folderId, Labels.ForError(ProviderErrorKind.NetworkError)) };
        }
        catch (ProviderException ex)
        {
            _log.Warn($"Provider '{provider.Id}' failed listing {folder.Kind}: {ex.Kind}: {ex.Message}");
            return new[] { Message(folderId, Labels.ForError(ex.Kind)) };
        }
        catch (Exception ex)
        {
            // Unexpected bug in a provider: contain it, log type only (messages may hold URLs).
            _log.Error($"Provider '{provider.Id}' threw {ex.GetType().Name} listing {folder.Kind}.", ex);
            return new[] { Message(folderId, Labels.ForError(ProviderErrorKind.Unknown)) };
        }

        _cache.Set(folderId, items, settings.MetadataCacheTtl);
        return items;
    }

    private IReadOnlyList<NavItem> GetRoot()
    {
        var items = _registry.Enabled
            .Select(p => (NavItem)new NavFolder(ItemIds.Encode(new FolderId(FolderKind.Provider, p.Id)), p.DisplayName))
            .ToList();

        if (items.Count == 0)
            items.Add(Message(ItemIds.Encode(new FolderId(FolderKind.Info, "none")), Labels.NoProviders));

        return items;
    }

    private async Task<IReadOnlyList<NavItem>> LoadAsync(
        ITubeProvider provider, FolderId folder, EmbyTubeSettings settings, CancellationToken ct)
    {
        switch (folder.Kind)
        {
            case FolderKind.Provider:
                return ProviderMenu(provider, settings);

            case FolderKind.Categories:
                if (!provider.Capabilities.Categories)
                    return Array.Empty<NavItem>();
                var categories = await provider.GetCategoriesAsync(ct).ConfigureAwait(false);
                return categories
                    .Where(c => folder.CategoryId is null ? c.ParentId is null : c.ParentId == folder.CategoryId)
                    .Take(settings.MaxCategories > 0 ? settings.MaxCategories : int.MaxValue)
                    .Select(c => (NavItem)new NavFolder(
                        ItemIds.Encode(new FolderId(FolderKind.Browse, provider.Id) { CategoryId = c.Id }),
                        c.Name) { ImageUrl = c.ThumbnailUrl })
                    .ToList();

            case FolderKind.Browse:
                var page = await provider.BrowseAsync(new BrowseRequest
                {
                    CategoryId = folder.CategoryId,
                    Sort = folder.Sort,
                    PageToken = folder.PageToken,
                    PageSize = settings.PageSize,
                }, ct).ConfigureAwait(false);
                return WithNextPage(provider, folder, page, settings);

            case FolderKind.Search:
                if (!provider.Capabilities.Search || string.IsNullOrWhiteSpace(folder.Query))
                    return Array.Empty<NavItem>();
                var results = await provider.SearchAsync(new SearchRequest(folder.Query)
                {
                    PageToken = folder.PageToken,
                    PageSize = settings.PageSize,
                }, ct).ConfigureAwait(false);
                return WithNextPage(provider, folder, results, settings);

            default:
                return Array.Empty<NavItem>();
        }
    }

    private static IReadOnlyList<NavItem> ProviderMenu(ITubeProvider provider, EmbyTubeSettings settings)
    {
        var caps = provider.Capabilities;
        var items = new List<NavItem>();

        if (caps.Browse)
        {
            if (caps.Sorts.Contains(TubeSorts.Popular))
                items.Add(Browse(provider, Labels.Popular, TubeSorts.Popular));
            if (caps.Sorts.Contains(TubeSorts.Newest))
                items.Add(Browse(provider, Labels.Newest, TubeSorts.Newest));
            if (caps.Sorts.Contains(TubeSorts.Featured))
                items.Add(Browse(provider, Labels.Featured, TubeSorts.Featured));
            if (caps.Sorts.Count == 0)
                items.Add(Browse(provider, Labels.AllVideos, null));
        }

        if (caps.Categories)
            items.Add(new NavFolder(ItemIds.Encode(new FolderId(FolderKind.Categories, provider.Id)), Labels.Categories));

        if (caps.Search)
        {
            foreach (var q in settings.SavedSearches.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var query = q.Trim();
                items.Add(new NavFolder(
                    ItemIds.Encode(new FolderId(FolderKind.Search, provider.Id) { Query = query }),
                    Labels.SearchPrefix + query));
            }
        }

        return items;
    }

    private static NavFolder Browse(ITubeProvider provider, string name, string? sort) =>
        new(ItemIds.Encode(new FolderId(FolderKind.Browse, provider.Id) { Sort = sort }), name);

    private static IReadOnlyList<NavItem> WithNextPage(
        ITubeProvider provider, FolderId folder, Page<TubeVideo> page, EmbyTubeSettings settings)
    {
        var items = new List<NavItem>(page.Items.Count + 1);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in page.Items)
        {
            if (v.ProviderId != provider.Id || string.IsNullOrEmpty(v.Id) || !seen.Add(v.Id))
                continue;
            items.Add(new NavVideo(ItemIds.Encode(new VideoId(provider.Id, v.Id)), v));
        }

        var maxPages = folder.CategoryId is not null ? settings.CategoryMaxPages : settings.MaxPages;
        if (page.HasMore && provider.Capabilities.Pagination && folder.PageNumber < maxPages)
        {
            var next = folder with { PageToken = page.NextPageToken, PageNumber = folder.PageNumber + 1 };
            items.Add(new NavFolder(ItemIds.Encode(next), Labels.NextPage(next.PageNumber)));
        }

        return items;
    }

    private static NavMessage Message(string parentId, string text) =>
        new(ItemIds.Encode(new FolderId(FolderKind.Info, "msg") { Query = parentId }), text);
}
