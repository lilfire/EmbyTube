using EmbyTube.Providers.Html;
using EmbyTube.Providers.Mock;
using EmbyTube.Providers.Pornhat;
using EmbyTube.Providers.Pornhub;
using EmbyTube.Providers.XHamster;
using EmbyTube.Providers.XVideos;

namespace EmbyTube.Plugin;

/// <summary>A source the admin can switch on, as shown on the settings page.</summary>
public class ProviderOption
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Single list of sources shown in the settings page. The page reads it from the plugin
/// configuration (<see cref="PluginConfiguration.AvailableProviders"/>), so adding a
/// provider here is all the UI needs. <see cref="EmbyTubeRuntime"/> logs a warning if a
/// constructed provider is missing from this list.
/// </summary>
internal static class ProviderCatalog
{
    public static ProviderOption[] Options => new[]
    {
        new ProviderOption { Id = MockProvider.ProviderId, Name = "EmbyTube Test Source (synthetic, open-licensed test streams)" },
        new ProviderOption { Id = XVideosProvider.ProviderId, Name = "XVideos (adult)" },
        new ProviderOption { Id = XHamsterProvider.ProviderId, Name = "xHamster (adult)" },
        new ProviderOption { Id = XnxxProvider.ProviderId, Name = "XNXX (adult)" },
        new ProviderOption { Id = PornhubProvider.ProviderId, Name = "Pornhub (adult, experimental: the site's CDN often refuses playback to Emby)" },
    }
    .Concat(PornhatSite.All.Select(s => new ProviderOption { Id = s.Id, Name = s.DisplayName + " (adult, Pornhat network)" }))
    .Concat(HtmlTubeSites.All.Select(s => new ProviderOption { Id = s.Id, Name = s.DisplayName + " (adult)" }))
    .ToArray();
}
