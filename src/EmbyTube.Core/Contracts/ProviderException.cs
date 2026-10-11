namespace EmbyTube.Core.Contracts;

public enum ProviderErrorKind
{
    Unknown,
    Unavailable,
    RateLimited,
    AccessDenied,
    Unsupported,
    ParseChanged,
    MediaExpired,
    NetworkError,
    NotFound,
    Blocked,
}

/// <summary>
/// Typed provider failure. Messages must be safe to log and show to users:
/// no full URLs, cookies, tokens or search terms.
/// </summary>
public sealed class ProviderException : Exception
{
    public ProviderException(ProviderErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    public ProviderErrorKind Kind { get; }

    public TimeSpan? RetryAfter { get; init; }
}
