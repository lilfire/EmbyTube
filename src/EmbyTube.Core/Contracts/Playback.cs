namespace EmbyTube.Core.Contracts;

public enum StreamProtocol
{
    /// <summary>Progressive HTTP(S) download of a single file (e.g. MP4).</summary>
    Progressive,

    /// <summary>HTTP Live Streaming (m3u8 manifest).</summary>
    Hls,
}

public sealed record PlaybackVariant(Uri Url, StreamProtocol Protocol, string Container)
{
    /// <summary>Vertical resolution in pixels, when known.</summary>
    public int? Height { get; init; }

    /// <summary>Total bitrate in bits/s, when known.</summary>
    public int? Bitrate { get; init; }

    /// <summary>Minimal headers the source requires. Never logged.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = BrowseRequest.EmptyFilters;

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Diagnostic only: the source needs something Emby clients cannot send directly.</summary>
    public bool RequiresProxy { get; init; }

    public string? DiagnosticCode { get; init; }
}

public sealed record PlaybackResult(IReadOnlyList<PlaybackVariant> Variants)
{
    /// <summary>Duration reported by the source at resolve time, if any.</summary>
    public TimeSpan? Duration { get; init; }
}
