using EmbyTube.Application;
using MediaBrowser.Model.Logging;

namespace EmbyTube.Plugin;

/// <summary>Adapts Emby's logger. Messages are passed as a format argument so braces are never interpreted.</summary>
internal sealed class EmbyLog : IEmbyTubeLog
{
    private readonly ILogger _logger;

    public EmbyLog(ILogger logger)
    {
        _logger = logger;
    }

    public void Debug(string message)
    {
        if (Plugin.Instance?.Configuration.DebugLogging == true)
            _logger.Info("[EmbyTube:debug] {0}", message);
    }

    public void Info(string message) => _logger.Info("[EmbyTube] {0}", message);

    public void Warn(string message) => _logger.Warn("[EmbyTube] {0}", message);

    // Exception messages from HTTP stacks can contain URLs: log the type and stack only.
    public void Error(string message, Exception? ex = null) =>
        _logger.Error("[EmbyTube] {0}{1}", message, ex is null ? string.Empty : $" ({ex.GetType().FullName})\n{ex.StackTrace}");
}
