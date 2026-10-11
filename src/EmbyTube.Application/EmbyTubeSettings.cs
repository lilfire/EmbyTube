namespace EmbyTube.Application;

/// <summary>Runtime settings, mapped from the Emby plugin configuration by the host.</summary>
public sealed record EmbyTubeSettings
{
    public int PageSize { get; init; } = 30;

    /// <summary>
    /// Maximum pages per listing. Emby's channel refresh walks every folder including
    /// "Next page", so this bounds requests per refresh (verified on Emby 4.10.1).
    /// </summary>
    public int MaxPages { get; init; } = 3;

    /// <summary>Maximum categories shown per source (0 = all). Each one is crawled on refresh.</summary>
    public int MaxCategories { get; init; } = 40;

    /// <summary>Pages per category listing (categories are numerous; keep this lower than MaxPages).</summary>
    public int CategoryMaxPages { get; init; } = 1;

    public TimeSpan MetadataCacheTtl { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan ProviderTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Highest preferred vertical resolution; 0 = no limit.</summary>
    public int MaxHeight { get; init; } = 1080;

    public bool PreferHls { get; init; }

    /// <summary>Saved searches shown as folders under each search-capable provider.</summary>
    public IReadOnlyList<string> SavedSearches { get; init; } = Array.Empty<string>();

    public static EmbyTubeSettings Default { get; } = new();
}
