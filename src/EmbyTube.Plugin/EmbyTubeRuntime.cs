using EmbyTube.Application;
using EmbyTube.Application.Navigation;
using EmbyTube.Application.Playback;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Security;
using EmbyTube.Core.Net;
using EmbyTube.Providers.Html;
using EmbyTube.Providers.Mock;
using EmbyTube.Providers.Pornhat;
using EmbyTube.Providers.Pornhub;
using EmbyTube.Providers.XHamster;
using EmbyTube.Providers.XVideos;

namespace EmbyTube.Plugin;

/// <summary>Composition root: builds providers and services from the current plugin configuration.</summary>
internal sealed class EmbyTubeRuntime
{
    private readonly IEmbyTubeLog _log;
    private readonly object _gate = new();
    private State? _state;

    public EmbyTubeRuntime(IEmbyTubeLog log)
    {
        _log = log;
        Plugin.ConfigurationChanged += (_, _) => Reset();
    }

    public static PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public bool IsEnabled => Config.Enabled;

    public ChannelNavigator Navigator => Current.Navigator;

    public PlaybackService Playback => Current.Playback;

    public ProviderRegistry Registry => Current.Registry;

    private State Current
    {
        get
        {
            lock (_gate)
            {
                return _state ??= Build();
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _state = null;
        }

        _log.Info("Configuration changed; caches cleared.");
    }

    private State Build()
    {
        var config = Config;
        var settings = ToSettings(config);
        var http = SharedHttp(settings.ProviderTimeout);
        var providers = new List<ITubeProvider>
        {
            new MockProvider(new MockOptions
            {
                Mp4Url = ParseUrl(config.MockMp4Url),
                HlsUrl = ParseUrl(config.MockHlsUrl),
            }),
            new XVideosProvider(http, new XVideosOptions { SfwThumbnails = config.XVideosSfwThumbnails }),
            new XHamsterProvider(http),
            new XnxxProvider(http, config.XVideosSfwThumbnails),
            new PornhubProvider(http, UserAgent),
        };
        providers.AddRange(PornhatSite.All.Select(site => (ITubeProvider)new PornhatNetworkProvider(site, http)));
        providers.AddRange(HtmlTubeSites.All.Select(site => (ITubeProvider)new HtmlTubeProvider(site, http, userAgent: UserAgent)));

        foreach (var missing in providers.Select(p => p.Id).Except(ProviderCatalog.Options.Select(o => o.Id)))
            _log.Warn($"Provider '{missing}' is not in ProviderCatalog and cannot be enabled from the settings page.");

        var enabled = new HashSet<string>(config.EnabledProviders ?? Array.Empty<string>(), StringComparer.Ordinal);
        var registry = new ProviderRegistry(providers, id => enabled.Contains(id));
        return new State(
            registry,
            new ChannelNavigator(registry, () => settings, _log),
            new PlaybackService(registry, () => settings, new UrlGuard(), _log));
    }

    internal static EmbyTubeSettings ToSettings(PluginConfiguration c) => new()
    {
        PageSize = Math.Clamp(c.PageSize, 5, 100),
        MaxPages = Math.Clamp(c.MaxPages, 1, 100),
        MaxCategories = Math.Clamp(c.MaxCategories, 0, 500),
        CategoryMaxPages = Math.Clamp(c.CategoryMaxPages, 1, 100),
        ProviderTimeout = TimeSpan.FromSeconds(Math.Clamp(c.RequestTimeoutSeconds, 3, 120)),
        MetadataCacheTtl = TimeSpan.FromMinutes(Math.Clamp(c.MetadataCacheMinutes, 0, 24 * 60)),
        MaxHeight = Math.Max(0, c.MaxHeight),
        PreferHls = c.PreferHls,
        SavedSearches = (c.SavedSearches ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(50)
            .ToArray(),
    };

    /// <summary>
    /// One HTTP client per timeout value (connection pooling + per-host rate limit are
    /// shared across config reloads). 1 s spacing per host keeps refresh crawls gentle.
    /// </summary>
    private static SafeHttpClient SharedHttp(TimeSpan timeout)
    {
        lock (HttpClients)
        {
            if (!HttpClients.TryGetValue(timeout, out var client))
            {
                client = new SafeHttpClient(new SafeHttpOptions
                {
                    Timeout = timeout,
                    MinIntervalPerHost = TimeSpan.FromSeconds(1),
                    UserAgent = UserAgent,
                });
                HttpClients[timeout] = client;
            }

            return client;
        }
    }

    private static readonly Dictionary<TimeSpan, SafeHttpClient> HttpClients = new();

    /// <summary>Honest User-Agent for all requests, including stream headers handed to Emby.</summary>
    internal static readonly string UserAgent = "EmbyTube/" + typeof(EmbyTubeRuntime).Assembly.GetName().Version?.ToString(3);

    private static Uri? ParseUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? u
            : null;

    private sealed record State(ProviderRegistry Registry, ChannelNavigator Navigator, PlaybackService Playback);
}
