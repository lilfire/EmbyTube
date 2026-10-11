using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace EmbyTube.Plugin;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Master switch. Off by default: the channel shows only an explanatory entry until
    /// an administrator enables it. This is NOT access control; use Emby's per-user
    /// channel access for that (docs/security-and-privacy.md).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Provider ids that are switched on.</summary>
    public string[] EnabledProviders { get; set; } = { "mock" };

    public int PageSize { get; set; } = 30;

    public int MaxPages { get; set; } = 3;

    /// <summary>Maximum categories per source; 0 = all. Every category is fetched on each channel refresh.</summary>
    public int MaxCategories { get; set; } = 40;

    public int CategoryMaxPages { get; set; } = 1;

    public int RequestTimeoutSeconds { get; set; } = 20;

    public int MetadataCacheMinutes { get; set; } = 30;

    /// <summary>Preferred maximum vertical resolution; 0 = unlimited.</summary>
    public int MaxHeight { get; set; } = 1080;

    public bool PreferHls { get; set; }

    /// <summary>One saved search per line; shown as folders under search-capable sources.</summary>
    public string SavedSearches { get; set; } = string.Empty;

    /// <summary>Optional replacement test streams for the mock source (must be public http(s) URLs).</summary>
    public string MockMp4Url { get; set; } = string.Empty;

    public string MockHlsUrl { get; set; } = string.Empty;

    /// <summary>XVideos: use the site's SFW thumbnail variant instead of the regular one.</summary>
    public bool XVideosSfwThumbnails { get; set; }

    public bool DebugLogging { get; set; }

    /// <summary>
    /// Read-only list for the settings page, always taken from <see cref="ProviderCatalog"/>.
    /// Not persisted; values posted back by the page are ignored.
    /// </summary>
    [XmlIgnore]
    public ProviderOption[] AvailableProviders
    {
        get => ProviderCatalog.Options;
        set { }
    }
}
