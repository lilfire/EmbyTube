using System.Globalization;
using EmbyTube.Application;
using EmbyTube.Application.Navigation;
using EmbyTube.Application.Playback;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Ids;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;

namespace EmbyTube.Plugin;

/// <summary>
/// Emby adapter. Translates <see cref="ChannelNavigator"/> output into channel items and
/// resolves media sources on demand through <see cref="IRequiresMediaInfoCallback"/>, so
/// no short-lived stream URL is ever stored in Emby's channel cache.
/// </summary>
public class EmbyTubeChannel : IChannel, IRequiresMediaInfoCallback
{
    private readonly IEmbyTubeLog _log;
    private readonly EmbyTubeRuntime _runtime;

    public EmbyTubeChannel(ILogManager logManager)
    {
        _log = new EmbyLog(logManager.GetLogger("EmbyTube"));
        _runtime = new EmbyTubeRuntime(_log);
    }

    public string Name => "EmbyTube";

    public string Description => "Online video sources (adult content). Access is controlled per user in Emby.";

    /// <summary>Marks the channel as adult content so Emby parental controls apply.</summary>
    public ChannelParentalRating ParentalRating => ChannelParentalRating.Adult;

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        _log.Debug($"GetChannelItems folder={ItemIds.Describe(query.FolderId)} user={query.UserId} start={query.StartIndex} limit={query.Limit}");

        if (!_runtime.IsEnabled)
            return Result(new List<ChannelItemInfo> { MessageItem("disabled", Labels.PluginDisabled) });

        var nav = await _runtime.Navigator.GetItemsAsync(query.FolderId, cancellationToken).ConfigureAwait(false);
        var items = nav.Select(ToChannelItem).ToList();
        return Result(items);
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        if (!_runtime.IsEnabled)
            throw new InvalidOperationException("EmbyTube is disabled.");

        try
        {
            var resolved = await _runtime.Playback.ResolveAsync(id, cancellationToken).ConfigureAwait(false);
            return resolved.Variants.Select((v, i) => ToMediaSource(id, i, v, resolved.Duration)).ToList();
        }
        catch (ProviderException ex)
        {
            _log.Warn($"Playback resolve failed: {ex.Kind}: {ex.Message}");
            // Emby surfaces this as a playback error; the message is safe (no URLs).
            throw new InvalidOperationException(Labels.ForError(ex.Kind), ex);
        }
    }

    public IEnumerable<ImageType> GetSupportedChannelImages() => new[] { ImageType.Primary, ImageType.Thumb };

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        var stream = GetType().Assembly.GetManifestResourceStream("EmbyTube.Plugin.thumb.png");
        var response = new DynamicImageResponse();
        if (stream is not null)
        {
            response.Format = MediaBrowser.Model.Drawing.ImageFormat.Png;
            response.Stream = stream;
        }

        return Task.FromResult(response);
    }

    private static ChannelItemInfo ToChannelItem(NavItem item) => item switch
    {
        NavVideo v => VideoItem(v),
        NavFolder f => new ChannelItemInfo
        {
            Id = f.Id,
            Name = f.Name,
            Type = ChannelItemType.Folder,
            FolderType = ChannelFolderType.Container,
            ImageUrl = f.ImageUrl,
        },
        _ => MessageItem(item.Id, item.Name),
    };

    private static ChannelItemInfo VideoItem(NavVideo nav)
    {
        var v = nav.Video;
        return new ChannelItemInfo
        {
            Id = nav.Id,
            Name = v.Title,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            ContentType = ChannelMediaContentType.Clip,
            Overview = v.Description,
            ImageUrl = v.ThumbnailUrl,
            RunTimeTicks = v.Duration?.Ticks,
            PremiereDate = v.PublishedAt?.UtcDateTime,
            DateCreated = v.PublishedAt?.UtcDateTime,
            ProductionYear = v.PublishedAt?.Year,
            Tags = v.Tags?.ToList() ?? new List<string>(),
            ProviderIds = new ProviderIdDictionary { ["EmbyTube"] = nav.Id },
            // Empty on purpose: Emby must call GetChannelItemMediaInfo at playback start.
            MediaSources = new List<MediaSourceInfo>(),
        };
    }

    private static ChannelItemInfo MessageItem(string id, string text) => new()
    {
        Id = id.StartsWith("f1|", StringComparison.Ordinal) ? id : ItemIds.Encode(new FolderId(FolderKind.Info, "msg") { Query = id }),
        Name = text,
        Type = ChannelItemType.Folder,
    };

    internal static MediaSourceInfo ToMediaSource(string itemId, int index, PlaybackVariant v, TimeSpan? duration)
    {
        var isHls = v.Protocol == StreamProtocol.Hls;
        var source = new MediaSourceInfo
        {
            // Stable per item + variant slot; never derived from the (expiring) URL.
            Id = StableId(itemId, index),
            Name = v.Height is { } h ? string.Create(CultureInfo.InvariantCulture, $"{h}p {(isHls ? "HLS" : "MP4")}") : (isHls ? "HLS" : "MP4"),
            Path = v.Url.AbsoluteUri,
            Protocol = MediaProtocol.Http,
            IsRemote = true,
            Container = isHls ? "hls" : v.Container,
            RunTimeTicks = duration?.Ticks,
            Bitrate = v.Bitrate,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            IsInfiniteStream = false,
            RequiresOpening = false,
            RequiresClosing = false,
            Type = MediaSourceType.Default,
        };

        if (v.Headers.Count > 0)
        {
            // Clients cannot add these headers themselves, so direct play would fail;
            // Emby must fetch the stream server-side (direct stream / transcode).
            source.RequiredHttpHeaders = new Dictionary<string, string>(v.Headers, StringComparer.OrdinalIgnoreCase);
            source.SupportsDirectPlay = false;
        }

        return source;
    }

    private static string StableId(string itemId, int index)
    {
        // Deterministic FNV-1a, hex; Emby expects compact ids without separators.
        ulong hash = 14695981039346656037;
        foreach (var ch in itemId)
        {
            hash ^= ch;
            hash *= 1099511628211;
        }

        return string.Create(CultureInfo.InvariantCulture, $"et{hash:x16}{index}");
    }

    private static ChannelItemResult Result(List<ChannelItemInfo> items) =>
        new() { Items = items, TotalRecordCount = items.Count };
}
