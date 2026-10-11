namespace EmbyTube.Application;

/// <summary>
/// Logging abstraction implemented by the Emby host. Callers must never pass
/// full URLs, headers, cookies, tokens or search terms.
/// </summary>
public interface IEmbyTubeLog
{
    void Debug(string message);

    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? ex = null);
}

public sealed class NullLog : IEmbyTubeLog
{
    public static NullLog Instance { get; } = new();

    public void Debug(string message) { }

    public void Info(string message) { }

    public void Warn(string message) { }

    public void Error(string message, Exception? ex = null) { }
}
