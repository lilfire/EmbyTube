using EmbyTube.Core.Contracts;
using EmbyTube.Core.Ids;
using EmbyTube.Core.Security;

namespace EmbyTube.Application.Playback;

/// <summary>Result of an on-demand resolve: variants ordered best-first, all SSRF-checked.</summary>
public sealed record ResolvedPlayback(VideoId Video, IReadOnlyList<PlaybackVariant> Variants)
{
    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// Resolves stream URLs at playback start (never at indexing time) and picks
/// variants. Results are intentionally not cached: signed URLs expire, and a
/// fresh resolve on every playback request is what makes replay after expiry work.
/// </summary>
public sealed class PlaybackService
{
    /// <summary>One controlled retry for transient failures. Never loops.</summary>
    private const int MaxAttempts = 2;

    private readonly ProviderRegistry _registry;
    private readonly Func<EmbyTubeSettings> _settings;
    private readonly UrlGuard _guard;
    private readonly IEmbyTubeLog _log;

    public PlaybackService(ProviderRegistry registry, Func<EmbyTubeSettings> settings, UrlGuard guard, IEmbyTubeLog log)
    {
        _registry = registry;
        _settings = settings;
        _guard = guard;
        _log = log;
    }

    public async Task<ResolvedPlayback> ResolveAsync(string itemId, CancellationToken ct)
    {
        if (!ItemIds.TryDecodeVideo(itemId, out var video) || video is null)
            throw new ProviderException(ProviderErrorKind.NotFound, "Not an EmbyTube video id.");

        var provider = _registry.GetEnabled(video.ProviderId)
            ?? throw new ProviderException(ProviderErrorKind.Unavailable, $"Provider '{video.ProviderId}' is disabled or unknown.");

        var settings = _settings();
        PlaybackResult? result = null;
        for (var attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(settings.ProviderTimeout);
            try
            {
                result = await provider.ResolvePlaybackAsync(video.Id, timeout.Token).ConfigureAwait(false);
                break;
            }
            catch (ProviderException ex) when (attempt < MaxAttempts && IsTransient(ex.Kind))
            {
                _log.Warn($"Resolve on '{provider.Id}' failed ({ex.Kind}); retrying once.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ProviderException(ProviderErrorKind.NetworkError, $"Resolve on '{provider.Id}' timed out.");
            }
        }

        var allowed = new List<PlaybackVariant>(result.Variants.Count);
        foreach (var variant in result.Variants)
        {
            try
            {
                await _guard.EnsureAllowedAsync(variant.Url, provider.AllowedHosts, ct).ConfigureAwait(false);
                allowed.Add(variant);
            }
            catch (ProviderException ex) when (ex.Kind == ProviderErrorKind.Blocked)
            {
                _log.Warn($"Dropped variant from '{provider.Id}': {ex.Message}");
            }
        }

        if (allowed.Count == 0)
        {
            throw new ProviderException(
                result.Variants.Count == 0 ? ProviderErrorKind.Unsupported : ProviderErrorKind.Blocked,
                $"No playable variant from '{provider.Id}'.");
        }

        var ordered = Order(allowed, settings);
        _log.Info($"Resolved '{provider.Id}' video: {ordered.Count} variant(s), best={Describe(ordered[0])}.");
        return new ResolvedPlayback(video, ordered) { Duration = result.Duration };
    }

    private static bool IsTransient(ProviderErrorKind kind) =>
        kind is ProviderErrorKind.NetworkError or ProviderErrorKind.Unavailable or ProviderErrorKind.MediaExpired;

    /// <summary>
    /// Preference: variants within MaxHeight first (highest first), then the
    /// preferred protocol, then bitrate. Over-limit variants are kept last as fallback.
    /// </summary>
    public static IReadOnlyList<PlaybackVariant> Order(IEnumerable<PlaybackVariant> variants, EmbyTubeSettings settings)
    {
        var preferred = settings.PreferHls ? StreamProtocol.Hls : StreamProtocol.Progressive;
        bool WithinLimit(PlaybackVariant v) => settings.MaxHeight <= 0 || (v.Height ?? 0) <= settings.MaxHeight;

        return variants
            .OrderByDescending(WithinLimit)
            .ThenByDescending(v => WithinLimit(v) ? v.Height ?? 0 : -(v.Height ?? 0))
            .ThenByDescending(v => v.Protocol == preferred)
            .ThenByDescending(v => v.Bitrate ?? 0)
            .ToList();
    }

    public static string Describe(PlaybackVariant v) =>
        $"{v.Protocol}/{v.Container}/{(v.Height is { } h ? h + "p" : "?p")} @ {LogSafe.Host(v.Url)}";
}
