using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace EmbyTube.Plugin;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
{
    public static readonly Guid PluginId = Guid.Parse("6f1d7a3e-2b8c-4e51-9a0d-3c7e5b2f8a14");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    /// <summary>Raised after the admin saves settings, so caches can be invalidated.</summary>
    public static event EventHandler? ConfigurationChanged;

    public override string Name => "EmbyTube";

    public override string Description => "Streams videos from modular online sources as an Emby channel.";

    public override Guid Id => PluginId;

    public ImageFormat ThumbImageFormat => ImageFormat.Png;

    public Stream GetThumbImage() =>
        GetType().Assembly.GetManifestResourceStream("EmbyTube.Plugin.thumb.png")
        ?? Stream.Null;

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = "embytube",
            EmbeddedResourcePath = "EmbyTube.Plugin.Configuration.config.html",
            IsMainConfigPage = true,
        },
        new PluginPageInfo
        {
            Name = "embytubejs",
            EmbeddedResourcePath = "EmbyTube.Plugin.Configuration.config.js",
        },
    };

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        base.UpdateConfiguration(configuration);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
