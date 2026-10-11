namespace EmbyTube.Core.Contracts;

/// <summary>
/// A content source. Providers know nothing about Emby: they deal in
/// <see cref="TubeVideo"/>/<see cref="TubeCategory"/> and return
/// <see cref="PlaybackResult"/> on demand. Failures are reported by throwing
/// <see cref="ProviderException"/>, never by silently returning empty results.
/// </summary>
public interface ITubeProvider
{
    /// <summary>Stable, lowercase id used in persistent Emby item ids. Must not contain ':' or '|'.</summary>
    string Id { get; }

    string DisplayName { get; }

    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Hosts (exact or suffix match, e.g. "example.com" also allows "cdn.example.com")
    /// this provider may contact or hand to Emby for metadata, images and playback.
    /// </summary>
    IReadOnlyCollection<string> AllowedHosts { get; }

    Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct);

    Task<Page<TubeVideo>> BrowseAsync(BrowseRequest request, CancellationToken ct);

    Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct);

    Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct);

    Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct);
}
