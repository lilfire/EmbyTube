namespace EmbyTube.Core.Security;

/// <summary>Helpers that strip sensitive parts (path, query, credentials) before logging.</summary>
public static class LogSafe
{
    public static string Host(Uri? url) =>
        url is null || !url.IsAbsoluteUri ? "(invalid-url)" : $"{url.Scheme}://{url.IdnHost}";

    public static string Host(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? Host(u) : "(invalid-url)";
}
