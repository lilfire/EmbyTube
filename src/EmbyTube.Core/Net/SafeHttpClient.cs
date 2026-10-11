using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Security;

namespace EmbyTube.Core.Net;

/// <summary>Final URL after redirects and its Content-Type (if the server sent one).</summary>
public sealed record ResolvedStream(Uri Url, string? ContentType)
{
    /// <summary>True for HLS playlists, by Content-Type or file extension.</summary>
    public bool IsHls =>
        (ContentType?.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ?? false)
        || Url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
}

public sealed record SafeHttpOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxRedirects { get; init; } = 5;

    /// <summary>Minimum spacing between requests to the same host.</summary>
    public TimeSpan MinIntervalPerHost { get; init; } = TimeSpan.FromMilliseconds(500);

    public string UserAgent { get; init; } = "EmbyTube/0.1";
}

/// <summary>
/// The only HTTP client providers may use. Enforces the host allowlist on every
/// hop, refuses to connect to non-public addresses (checked on the actual socket
/// address, so DNS rebinding cannot bypass it), follows redirects manually,
/// bounds response size and time, rate-limits per host and maps HTTP failures to
/// typed <see cref="ProviderException"/>s.
/// </summary>
public sealed class SafeHttpClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly SafeHttpOptions _options;
    private readonly ConcurrentDictionary<string, HostGate> _gates = new(StringComparer.OrdinalIgnoreCase);

    public SafeHttpClient(SafeHttpOptions? options = null, HttpMessageHandler? handler = null)
    {
        _options = options ?? new SafeHttpOptions();
        _http = new HttpClient(handler ?? CreateHandler(), disposeHandler: true)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan, // enforced per request via CTS
        };
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = ConnectPublicOnlyAsync,
    };

    private static async ValueTask<Stream> ConnectPublicOnlyAsync(SocketsHttpConnectionContext ctx, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct).ConfigureAwait(false);

        // Prefer IPv4: CDNs sign stream URLs for the client address that loaded the page.
        // Many sites are IPv4-only while their CDN is dual-stack, so mixing families makes the
        // signed URL fail (observed: Pornhub HLS → 410). Emby itself fetches streams over IPv4
        // in typical setups, so this also keeps plugin and server on the same address.
        var target = addresses
            .Where(UrlGuard.IsPublic)
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .FirstOrDefault();
        if (target is null || addresses.Any(a => !UrlGuard.IsPublic(a)))
            throw new ProviderException(ProviderErrorKind.Blocked, $"Refused connection to non-public address for {ctx.DnsEndPoint.Host}.");

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, ctx.DnsEndPoint.Port), ct).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task<string> GetStringAsync(
        Uri url,
        IReadOnlyCollection<string> allowedHosts,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Timeout);
        var token = timeout.Token;

        try
        {
            var current = url;
            for (var hop = 0; ; hop++)
            {
                if (!UrlGuard.IsAllowedSyntax(current, allowedHosts, out var reason))
                    throw new ProviderException(ProviderErrorKind.Blocked, $"Blocked URL to {LogSafe.Host(current)} ({reason}).");

                await GateFor(current.IdnHost).WaitAsync(_options.MinIntervalPerHost, token).ConfigureAwait(false);

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                if (headers is null || !headers.Keys.Any(k => k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                    request.Headers.UserAgent.ParseAdd(_options.UserAgent);
                if (headers is not null)
                {
                    foreach (var (k, v) in headers)
                        request.Headers.TryAddWithoutValidation(k, v);
                }

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                if (IsRedirect(response.StatusCode))
                {
                    if (hop >= _options.MaxRedirects || response.Headers.Location is null)
                        throw new ProviderException(ProviderErrorKind.NetworkError, $"Too many or invalid redirects from {LogSafe.Host(current)}.");
                    current = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(current, response.Headers.Location);
                    continue;
                }

                ThrowForStatus(response, current);
                return await ReadBoundedAsync(response.Content, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ProviderException(ProviderErrorKind.NetworkError, $"Timeout contacting {LogSafe.Host(url)}.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is ProviderException pe)
        {
            throw pe;
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ProviderErrorKind.NetworkError, $"Network error contacting {LogSafe.Host(url)}.", ex);
        }
    }

    /// <summary>
    /// Follows redirects (allowlist checked on every hop) and returns the final URL without
    /// reading the body. Used to hand Emby the real CDN address instead of a site redirector,
    /// so the SSRF check covers the URL Emby will actually fetch.
    /// </summary>
    public async Task<Uri> ResolveRedirectsAsync(
        Uri url,
        IReadOnlyCollection<string> allowedHosts,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null) =>
        (await ResolveStreamAsync(url, allowedHosts, ct, headers).ConfigureAwait(false)).Url;

    /// <summary>Like <see cref="ResolveRedirectsAsync"/>, also returning the final response's Content-Type.</summary>
    public async Task<ResolvedStream> ResolveStreamAsync(
        Uri url,
        IReadOnlyCollection<string> allowedHosts,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Timeout);
        var token = timeout.Token;

        try
        {
            var current = url;
            for (var hop = 0; ; hop++)
            {
                if (!UrlGuard.IsAllowedSyntax(current, allowedHosts, out var reason))
                    throw new ProviderException(ProviderErrorKind.Blocked, $"Blocked URL to {LogSafe.Host(current)} ({reason}).");

                await GateFor(current.IdnHost).WaitAsync(_options.MinIntervalPerHost, token).ConfigureAwait(false);

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                if (headers is null || !headers.Keys.Any(k => k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                    request.Headers.UserAgent.ParseAdd(_options.UserAgent);
                if (headers is not null)
                {
                    foreach (var (k, v) in headers)
                        request.Headers.TryAddWithoutValidation(k, v);
                }

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                if (IsRedirect(response.StatusCode))
                {
                    if (hop >= _options.MaxRedirects || response.Headers.Location is null)
                        throw new ProviderException(ProviderErrorKind.NetworkError, $"Too many or invalid redirects from {LogSafe.Host(current)}.");
                    current = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(current, response.Headers.Location);
                    continue;
                }

                ThrowForStatus(response, current);
                // Body is never read; disposing the response aborts the transfer.
                return new ResolvedStream(current, response.Content.Headers.ContentType?.MediaType);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ProviderException(ProviderErrorKind.NetworkError, $"Timeout contacting {LogSafe.Host(url)}.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is ProviderException pe)
        {
            throw pe;
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ProviderErrorKind.NetworkError, $"Network error contacting {LogSafe.Host(url)}.", ex);
        }
    }

    private static bool IsRedirect(HttpStatusCode code) =>
        code is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static void ThrowForStatus(HttpResponseMessage response, Uri url)
    {
        if (response.IsSuccessStatusCode)
            return;

        var host = LogSafe.Host(url);
        var code = (int)response.StatusCode;
        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound or HttpStatusCode.Gone =>
                new ProviderException(ProviderErrorKind.NotFound, $"{host} returned {code}."),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new ProviderException(ProviderErrorKind.AccessDenied, $"{host} returned {code}."),
            HttpStatusCode.TooManyRequests =>
                new ProviderException(ProviderErrorKind.RateLimited, $"{host} returned 429.") { RetryAfter = RetryAfter(response.Headers.RetryAfter) },
            HttpStatusCode.ServiceUnavailable =>
                new ProviderException(ProviderErrorKind.Unavailable, $"{host} returned 503.") { RetryAfter = RetryAfter(response.Headers.RetryAfter) },
            _ => new ProviderException(code >= 500 ? ProviderErrorKind.Unavailable : ProviderErrorKind.Unknown, $"{host} returned {code}."),
        };
    }

    internal static TimeSpan? RetryAfter(RetryConditionHeaderValue? value)
    {
        if (value?.Delta is { } delta)
            return delta;
        if (value?.Date is { } date)
        {
            var d = date - DateTimeOffset.UtcNow;
            return d > TimeSpan.Zero ? d : TimeSpan.Zero;
        }

        return null;
    }

    private async Task<string> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > _options.MaxResponseBytes)
            throw new ProviderException(ProviderErrorKind.Unsupported, "Response too large.");

        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > _options.MaxResponseBytes)
                throw new ProviderException(ProviderErrorKind.Unsupported, "Response too large.");
            buffer.Write(chunk, 0, read);
        }

        var charset = content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try
        {
            encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
        }

        return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private HostGate GateFor(string host) => _gates.GetOrAdd(host, _ => new HostGate());

    public void Dispose() => _http.Dispose();

    /// <summary>Serializes requests per host and spaces them by a minimum interval.</summary>
    private sealed class HostGate
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DateTimeOffset _next = DateTimeOffset.MinValue;

        public async Task WaitAsync(TimeSpan interval, CancellationToken ct)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var delay = _next - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                _next = DateTimeOffset.UtcNow + interval;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
