using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Security;

namespace EmbyTube.Core.Tests;

public class UrlGuardTests
{
    private static readonly string[] Allowed = { "example.com" };

    [Theory]
    [InlineData("https://example.com/v.mp4")]
    [InlineData("https://cdn.example.com/v.mp4")]
    [InlineData("http://example.com:8080/v.mp4")]
    [InlineData("https://EXAMPLE.com./v.mp4")]
    public void Allowlisted_urls_pass_syntax_check(string url)
    {
        Assert.True(UrlGuard.IsAllowedSyntax(new Uri(url), Allowed, out var reason), reason);
    }

    [Theory]
    [InlineData("https://evil.com/v.mp4", "host-not-allowlisted")]
    [InlineData("https://example.com.evil.com/v.mp4", "host-not-allowlisted")]
    [InlineData("https://notexample.com/v.mp4", "host-not-allowlisted")]
    [InlineData("ftp://example.com/v.mp4", "scheme")]
    [InlineData("file:///etc/passwd", "scheme")]
    [InlineData("https://user:pw@example.com/v.mp4", "userinfo")]
    [InlineData("https://example.com:22/v.mp4", "port")]
    [InlineData("http://127.0.0.1/", "private-ip")]
    [InlineData("http://169.254.169.254/latest/meta-data/", "private-ip")]
    [InlineData("http://10.0.0.5/", "private-ip")]
    [InlineData("http://[::1]/", "private-ip")]
    [InlineData("http://[fd00::1]/", "private-ip")]
    public void Disallowed_urls_fail_syntax_check(string url, string expectedReason)
    {
        Assert.False(UrlGuard.IsAllowedSyntax(new Uri(url), Allowed, out var reason));
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fd12:3456::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("64:ff9b::a00:1", false)]
    [InlineData("2001:db8::1", false)]
    public void IsPublic_classifies_addresses(string ip, bool expected)
    {
        Assert.Equal(expected, UrlGuard.IsPublic(IPAddress.Parse(ip)));
    }

    [Fact]
    public async Task Dns_rebinding_to_private_address_is_blocked()
    {
        var guard = new UrlGuard((_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.0.10") }));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            guard.EnsureAllowedAsync(new Uri("https://cdn.example.com/x.m3u8"), Allowed, CancellationToken.None));

        Assert.Equal(ProviderErrorKind.Blocked, ex.Kind);
        Assert.DoesNotContain("/x.m3u8", ex.Message); // message never carries the path
    }

    [Fact]
    public async Task Public_resolution_is_allowed()
    {
        var guard = new UrlGuard((_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") }));
        await guard.EnsureAllowedAsync(new Uri("https://cdn.example.com/x.m3u8"), Allowed, CancellationToken.None);
    }

    [Fact]
    public void LogSafe_strips_path_query_and_credentials()
    {
        Assert.Equal("https://cdn.example.com", LogSafe.Host("https://u:p@cdn.example.com/a/b.mp4?token=secret"));
        Assert.Equal("(invalid-url)", LogSafe.Host("not a url"));
    }
}
