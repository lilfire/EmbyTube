using System.Globalization;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Providers.Mock;

/// <summary>
/// Synthetic provider used to prove navigation, metadata, pagination and playback
/// end to end without touching any real tube site. All media are openly licensed
/// test streams (Blender Foundation open movies, CC BY; Apple/Mux public HLS test
/// streams). See docs/licensing.md.
/// </summary>
public sealed class MockProvider : ITubeProvider
{
    public const string ProviderId = "mock";
    public const int VideosPerListing = 95;

    private static readonly IReadOnlyList<MockStream> DefaultStreams = new[]
    {
        new MockStream("bbb-mp4", "Big Buck Bunny (MP4, 10 min)", StreamProtocol.Progressive, "mp4", 720,
            new Uri("https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4"),
            TimeSpan.FromSeconds(596), new Uri("https://archive.org/services/img/BigBuckBunny_124")),
        new MockStream("sintel-mp4", "Sintel (MP4, low bitrate)", StreamProtocol.Progressive, "mp4", 480,
            new Uri("https://archive.org/download/Sintel/sintel-2048-stereo_512kb.mp4"),
            TimeSpan.FromSeconds(888), new Uri("https://archive.org/services/img/Sintel")),
        new MockStream("short-mp4", "Big Buck Bunny (MP4, 10 s clip)", StreamProtocol.Progressive, "mp4", 720,
            new Uri("https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/720/Big_Buck_Bunny_720_10s_1MB.mp4"),
            TimeSpan.FromSeconds(10), new Uri("https://archive.org/services/img/BigBuckBunny_124")),
        new MockStream("bbb-hls", "Big Buck Bunny (HLS, adaptive)", StreamProtocol.Hls, "hls", 720,
            new Uri("https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
            TimeSpan.FromSeconds(635), new Uri("https://archive.org/services/img/BigBuckBunny_124")),
        new MockStream("bipbop-hls", "Apple bipbop (HLS, adaptive)", StreamProtocol.Hls, "hls", 1080,
            new Uri("https://devstreaming-cdn.apple.com/videos/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8"),
            TimeSpan.FromSeconds(1800), new Uri("https://archive.org/services/img/ElephantsDream")),
    };

    private static readonly IReadOnlyList<TubeCategory> CategoryList = new[]
    {
        new TubeCategory("mp4", "Progressive MP4"),
        new TubeCategory("hls", "HLS"),
        new TubeCategory("multi", "Multiple variants (quality selection)"),
        new TubeCategory("errors", "Error cases"),
    };

    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IReadOnlyList<MockStream> _streams;
    private readonly Func<DateTimeOffset> _now;
    private long _resolveCount;

    public MockProvider(MockOptions? options = null, Func<DateTimeOffset>? now = null)
    {
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _streams = ApplyOverrides(options ?? new MockOptions());
        AllowedHosts = _streams.SelectMany(s => new[] { s.Url.IdnHost, s.Thumbnail.IdnHost })
            .Concat(new[] { "archive.org" }) // archive.org redirects to dnNNN.*.archive.org nodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Id => ProviderId;

    public string DisplayName => "EmbyTube Test Source";

    public ProviderCapabilities Capabilities { get; } = new()
    {
        Browse = true,
        Categories = true,
        Search = true,
        Pagination = true,
        Sorts = new[] { TubeSorts.Popular, TubeSorts.Newest },
        Formats = new[] { StreamProtocol.Progressive, StreamProtocol.Hls },
    };

    public IReadOnlyCollection<string> AllowedHosts { get; }

    /// <summary>Number of playback resolves served; diagnostic for "resolve at playback start" tests.</summary>
    public long ResolveCount => Interlocked.Read(ref _resolveCount);

    public Task<IReadOnlyList<TubeCategory>> GetCategoriesAsync(CancellationToken ct) => Task.FromResult(CategoryList);

    public Task<Page<TubeVideo>> BrowseAsync(BrowseRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.CategoryId is not null && CategoryList.All(c => c.Id != request.CategoryId))
            throw new ProviderException(ProviderErrorKind.NotFound, "Unknown mock category.");

        var all = Enumerable.Range(0, VideosPerListing).Select(i => MakeVideo(request.CategoryId ?? "all", i));
        all = request.Sort == TubeSorts.Newest
            ? all.OrderByDescending(v => v.PublishedAt)
            : all; // "popular" = generation order
        return Task.FromResult(Paginate(all.ToList(), request.PageToken, request.PageSize));
    }

    public Task<Page<TubeVideo>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var matches = Enumerable.Range(0, VideosPerListing)
            .Select(i => MakeVideo("all", i))
            .Where(v => v.Title.Contains(request.Query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        return Task.FromResult(Paginate(matches, request.PageToken, request.PageSize));
    }

    public Task<TubeVideo?> GetVideoAsync(string videoId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(TryParseVideoId(videoId, out var category, out var index)
            ? MakeVideo(category, index)
            : null);
    }

    public Task<PlaybackResult> ResolvePlaybackAsync(string videoId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _resolveCount);

        if (!TryParseVideoId(videoId, out var category, out var index))
            throw new ProviderException(ProviderErrorKind.NotFound, "Unknown mock video.");

        if (category == "errors")
        {
            switch (index % 3)
            {
                case 0:
                    throw new ProviderException(ProviderErrorKind.Unavailable, "Simulated unavailable video.");
                case 1:
                    // Must be rejected by the SSRF guard before reaching Emby.
                    return Task.FromResult(new PlaybackResult(new[]
                    {
                        new PlaybackVariant(new Uri("http://169.254.169.254/latest/meta-data/"), StreamProtocol.Progressive, "mp4"),
                    }));
                default:
                    return Task.FromResult(new PlaybackResult(Array.Empty<PlaybackVariant>()));
            }
        }

        // A per-resolve token proves URLs are produced at playback start, not at index time.
        var token = _now().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var expires = _now().AddMinutes(10);

        IEnumerable<MockStream> streams = category == "multi"
            ? _streams.Where(s => s.Protocol == StreamProtocol.Progressive && s.Duration > TimeSpan.FromMinutes(1))
                .Concat(_streams.Where(s => s.Protocol == StreamProtocol.Hls).Take(1))
            : new[] { StreamFor(category, index) };

        var variants = streams.Select(s => new PlaybackVariant(WithToken(s.Url, token), s.Protocol, s.Container)
        {
            Height = s.Height,
            ExpiresAt = expires,
        }).ToList();

        return Task.FromResult(new PlaybackResult(variants) { Duration = StreamFor(category, index).Duration });
    }

    private TubeVideo MakeVideo(string category, int index)
    {
        var stream = StreamFor(category, index);
        var categoryName = CategoryList.FirstOrDefault(c => c.Id == category)?.Name ?? "All";
        var id = string.Create(CultureInfo.InvariantCulture, $"{category}-{index:D3}");
        return new TubeVideo(ProviderId, id, string.Create(CultureInfo.InvariantCulture, $"Test video {index + 1:D3} – {stream.Title}"))
        {
            Description = $"Synthetic EmbyTube test item.\nCategory: {categoryName}\nStream: {stream.Key} ({stream.Protocol}).",
            ThumbnailUrl = stream.Thumbnail.AbsoluteUri,
            Duration = category == "errors" ? null : stream.Duration,
            // Deterministic, so ids and metadata are stable between refreshes.
            PublishedAt = Epoch.AddHours(-((index * 37) % VideosPerListing) * 6),
            Tags = new[] { "test", stream.Protocol.ToString().ToLowerInvariant() },
        };
    }

    private MockStream StreamFor(string category, int index)
    {
        var pool = category switch
        {
            "mp4" => _streams.Where(s => s.Protocol == StreamProtocol.Progressive).ToList(),
            "hls" => _streams.Where(s => s.Protocol == StreamProtocol.Hls).ToList(),
            _ => _streams.ToList(),
        };
        return pool[index % pool.Count];
    }

    private static bool TryParseVideoId(string videoId, out string category, out int index)
    {
        category = string.Empty;
        index = -1;
        var dash = videoId.LastIndexOf('-');
        if (dash <= 0)
            return false;
        var parsed = videoId[..dash];
        category = parsed;
        if (parsed != "all" && CategoryList.All(c => c.Id != parsed))
            return false;
        return int.TryParse(videoId[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out index)
            && index < VideosPerListing;
    }

    private static Page<TubeVideo> Paginate(IReadOnlyList<TubeVideo> all, string? pageToken, int pageSize)
    {
        var offset = 0;
        if (pageToken is not null
            && (!int.TryParse(pageToken, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > all.Count))
            throw new ProviderException(ProviderErrorKind.ParseChanged, "Invalid page token.");

        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = all.Skip(offset).Take(pageSize).ToList();
        var next = offset + items.Count < all.Count
            ? (offset + items.Count).ToString(CultureInfo.InvariantCulture)
            : null;
        return new Page<TubeVideo>(items, next) { TotalCount = all.Count };
    }

    private static Uri WithToken(Uri url, string token)
    {
        var builder = new UriBuilder(url);
        var q = builder.Query.TrimStart('?');
        builder.Query = (q.Length > 0 ? q + "&" : string.Empty) + "embytube_t=" + token;
        return builder.Uri;
    }

    private static IReadOnlyList<MockStream> ApplyOverrides(MockOptions options)
    {
        var list = DefaultStreams.ToList();
        if (options.Mp4Url is { } mp4)
            list.Insert(0, new MockStream("custom-mp4", "Custom MP4", StreamProtocol.Progressive, "mp4", null, mp4, TimeSpan.FromMinutes(1), DefaultStreams[0].Thumbnail));
        if (options.HlsUrl is { } hls)
            list.Insert(0, new MockStream("custom-hls", "Custom HLS", StreamProtocol.Hls, "hls", null, hls, TimeSpan.FromMinutes(1), DefaultStreams[0].Thumbnail));
        return list;
    }

    private sealed record MockStream(
        string Key, string Title, StreamProtocol Protocol, string Container, int? Height, Uri Url, TimeSpan Duration, Uri Thumbnail);
}

/// <summary>Optional admin overrides so the mock does not depend on third-party test hosts.</summary>
public sealed record MockOptions
{
    public Uri? Mp4Url { get; init; }
    public Uri? HlsUrl { get; init; }
}
