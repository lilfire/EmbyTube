namespace EmbyTube.Core.Contracts;

public enum VideoAvailability
{
    Available,
    Unavailable,
    Unknown,
}

public sealed record TubeVideo(
    string ProviderId,
    string Id,
    string Title)
{
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public string? SourcePageUrl { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public VideoAvailability Availability { get; init; } = VideoAvailability.Available;
}

public sealed record TubeCategory(string Id, string Name)
{
    public string? ThumbnailUrl { get; init; }
    public string? ParentId { get; init; }
}

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextPageToken)
{
    public bool HasMore => NextPageToken is not null;
    public int? TotalCount { get; init; }

    public static Page<T> Empty { get; } = new(Array.Empty<T>(), null);
}

/// <summary>Well-known sort keys. Providers may support a subset (see <see cref="ProviderCapabilities.Sorts"/>).</summary>
public static class TubeSorts
{
    public const string Popular = "popular";
    public const string Newest = "newest";

    /// <summary>A curated/editorial selection (e.g. "today's selection").</summary>
    public const string Featured = "featured";
}

public sealed record BrowseRequest
{
    public string? CategoryId { get; init; }
    public string? Sort { get; init; }
    public IReadOnlyDictionary<string, string> Filters { get; init; } = EmptyFilters;
    public string? PageToken { get; init; }
    public int PageSize { get; init; } = 30;

    internal static readonly IReadOnlyDictionary<string, string> EmptyFilters = new Dictionary<string, string>();
}

public sealed record SearchRequest(string Query)
{
    public string? PageToken { get; init; }
    public int PageSize { get; init; } = 30;
    public IReadOnlyDictionary<string, string> Filters { get; init; } = BrowseRequest.EmptyFilters;
}

public sealed record ProviderCapabilities
{
    public bool Browse { get; init; }
    public bool Categories { get; init; }
    public bool Search { get; init; }
    public bool Pagination { get; init; }
    public bool Related { get; init; }
    public IReadOnlyList<string> Sorts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<StreamProtocol> Formats { get; init; } = Array.Empty<StreamProtocol>();
}
