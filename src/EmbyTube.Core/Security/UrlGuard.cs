using System.Net;
using System.Net.Sockets;
using EmbyTube.Core.Contracts;

namespace EmbyTube.Core.Security;

/// <summary>
/// SSRF protection. A URL is allowed only when it is http(s), on default-or-standard
/// ports, its host is on the provider allowlist, and every address it resolves to
/// is public. <see cref="SafeHttpClient"/> re-checks the address at connect time,
/// which also defeats DNS rebinding.
/// </summary>
public sealed class UrlGuard
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;

    public UrlGuard(Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    {
        _resolve = resolve ?? ((host, ct) => Dns.GetHostAddressesAsync(host, ct));
    }

    /// <summary>Syntactic check only (no DNS): scheme, port, credentials and allowlist.</summary>
    public static bool IsAllowedSyntax(Uri url, IReadOnlyCollection<string> allowedHosts, out string reason)
    {
        if (!url.IsAbsoluteUri)
        {
            reason = "relative-url";
            return false;
        }

        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            reason = "scheme";
            return false;
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            reason = "userinfo";
            return false;
        }

        if (!url.IsDefaultPort && url.Port is not (80 or 443 or 8080 or 8443))
        {
            reason = "port";
            return false;
        }

        if (url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            // IP literals are only acceptable if explicitly allowlisted and public.
            if (!IPAddress.TryParse(url.Host.Trim('[', ']'), out var ip) || !IsPublic(ip))
            {
                reason = "private-ip";
                return false;
            }
        }

        if (!IsHostAllowed(url.IdnHost, allowedHosts))
        {
            reason = "host-not-allowlisted";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static bool IsHostAllowed(string host, IReadOnlyCollection<string> allowedHosts)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        foreach (var raw in allowedHosts)
        {
            var allowed = raw.Trim().TrimEnd('.').ToLowerInvariant();
            if (allowed.Length == 0)
                continue;
            if (host == allowed || host.EndsWith("." + allowed, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Full check including DNS. Throws <see cref="ProviderException"/> (Blocked) when rejected.</summary>
    public async Task EnsureAllowedAsync(Uri url, IReadOnlyCollection<string> allowedHosts, CancellationToken ct)
    {
        if (!IsAllowedSyntax(url, allowedHosts, out var reason))
            throw Blocked(url, reason);

        if (url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
            return;

        IPAddress[] addresses;
        try
        {
            addresses = await _resolve(url.IdnHost, ct).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw new ProviderException(ProviderErrorKind.NetworkError, $"DNS lookup failed for {LogSafe.Host(url)}.", ex);
        }

        if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
            throw Blocked(url, "resolves-to-private-ip");
    }

    private static ProviderException Blocked(Uri url, string reason) =>
        new(ProviderErrorKind.Blocked, $"Blocked URL to {LogSafe.Host(url)} ({reason}).");

    /// <summary>True for globally routable unicast addresses.</summary>
    public static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return !(b[0] == 0                                   // "this" network
                || b[0] == 10                                    // RFC1918
                || b[0] == 127                                   // loopback
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)    // CGNAT
                || (b[0] == 169 && b[1] == 254)                  // link-local, cloud metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)     // RFC1918
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)       // IETF protocol assignments
                || (b[0] == 192 && b[1] == 0 && b[2] == 2)       // TEST-NET-1
                || (b[0] == 192 && b[1] == 168)                  // RFC1918
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))   // benchmarking
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)    // TEST-NET-2
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)     // TEST-NET-3
                || b[0] >= 224);                                 // multicast, reserved, broadcast
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any))
                return false;
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
                return false;

            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)                           // ULA fc00::/7
                return false;
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // documentation
                return false;
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B) // NAT64 can map to private v4
                return false;
            return (b[0] & 0xE0) == 0x20;                        // global unicast 2000::/3
        }

        return false;
    }
}
