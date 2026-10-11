using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace EmbyTube.Plugin;

/// <summary>
/// Emby (verified on 4.10.1) serves channel browsing only from its library index, which
/// is filled by RefreshChannelContent ("Refresh Internet Channels" task). Without this,
/// saved settings would not show up until the next scheduled refresh. We re-index the
/// channel shortly after each save, debounced and never concurrently.
/// </summary>
public sealed class ChannelRefreshEntryPoint : IServerEntryPoint
{
    private const int MaxRefreshLevel = 10;
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    private readonly IChannelManager _channelManager;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource _cts = new();
    private int _pending;

    public ChannelRefreshEntryPoint(IChannelManager channelManager, ILogManager logManager)
    {
        _channelManager = channelManager;
        _logger = logManager.GetLogger("EmbyTube");
    }

    public void Run() => Plugin.ConfigurationChanged += OnConfigurationChanged;

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
            return; // a refresh is already queued; it will pick up the latest settings

        _ = Task.Run(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        var ct = _cts.Token;
        try
        {
            await Task.Delay(Debounce, ct).ConfigureAwait(false);
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Interlocked.Exchange(ref _pending, 0);
                var channel = _channelManager.GetChannel<EmbyTubeChannel>();
                _logger.Info("[EmbyTube] {0}", "Re-indexing channel after settings change.");
                await _channelManager.RefreshChannelContent(channel, MaxRefreshLevel, null!, ct).ConfigureAwait(false);
                _logger.Info("[EmbyTube] {0}", "Channel re-index finished.");
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error("[EmbyTube] Channel re-index failed: {0}", ex.GetType().FullName);
        }
    }

    public void Dispose()
    {
        Plugin.ConfigurationChanged -= OnConfigurationChanged;
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }
}
