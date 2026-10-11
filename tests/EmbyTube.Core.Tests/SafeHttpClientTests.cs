using System.Net;
using EmbyTube.Core.Contracts;
using EmbyTube.Core.Net;

namespace EmbyTube.Core.Tests;

public class SafeHttpClientTests
{
    private static readonly string[] Allowed = { "example.com" };

    private static SafeHttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, SafeHttpOptions? options = null) =>
        new(options ?? new SafeHttpOptions { MinIntervalPerHost = TimeSpan.Zero }, new StubHandler(respond));

    [Fact]
    public async Task Returns_body_on_success()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello") });
        Assert.Equal("hello", await client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ProviderErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ProviderErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.Unauthorized, ProviderErrorKind.AccessDenied)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderErrorKind.Unavailable)]
    public async Task Maps_status_codes_to_typed_errors(HttpStatusCode status, ProviderErrorKind expected)
    {
        using var client = Client(_ => new HttpResponseMessage(status));
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/secret-path?q=term"), Allowed, CancellationToken.None));
        Assert.Equal(expected, ex.Kind);
        Assert.DoesNotContain("secret-path", ex.Message);
        Assert.DoesNotContain("term", ex.Message);
    }

    [Fact]
    public async Task Retry_after_is_reported()
    {
        using var client = Client(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return r;
        });
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(42), ex.RetryAfter);
    }

    [Fact]
    public async Task Follows_allowlisted_redirects()
    {
        using var client = Client(req => req.RequestUri!.AbsolutePath == "/a"
            ? Redirect("https://cdn.example.com/b")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        Assert.Equal("ok", await client.GetStringAsync(new Uri("https://example.com/a"), Allowed, CancellationToken.None));
    }

    [Theory]
    [InlineData("https://evil.com/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    public async Task Blocks_redirect_out_of_allowlist(string target)
    {
        var calls = 0;
        using var client = Client(_ =>
        {
            calls++;
            return Redirect(target);
        });
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Blocked, ex.Kind);
        Assert.Equal(1, calls); // the blocked target is never requested
    }

    [Fact]
    public async Task Redirect_loop_is_bounded()
    {
        var calls = 0;
        using var client = Client(_ =>
        {
            calls++;
            return Redirect("https://example.com/loop");
        });
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
        Assert.Equal(ProviderErrorKind.NetworkError, ex.Kind);
        Assert.Equal(6, calls);
    }

    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        using var client = Client(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 5000)) },
            new SafeHttpOptions { MaxResponseBytes = 1000, MinIntervalPerHost = TimeSpan.Zero });
        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
        Assert.Equal(ProviderErrorKind.Unsupported, ex.Kind);
    }

    [Fact]
    public async Task Timeout_becomes_network_error_and_caller_cancellation_propagates()
    {
        using var client = new SafeHttpClient(
            new SafeHttpOptions { Timeout = TimeSpan.FromMilliseconds(100), MinIntervalPerHost = TimeSpan.Zero },
            new StubHandler(_ => throw new InvalidOperationException("unused"), delay: TimeSpan.FromSeconds(10)));

        var ex = await Assert.ThrowsAsync<ProviderException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, CancellationToken.None));
        Assert.Equal(ProviderErrorKind.NetworkError, ex.Kind);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetStringAsync(new Uri("https://example.com/"), Allowed, cts.Token));
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var r = new HttpResponseMessage(HttpStatusCode.Found);
        r.Headers.Location = new Uri(location);
        return r;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        private readonly TimeSpan _delay;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond, TimeSpan delay = default)
        {
            _respond = respond;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);
            return _respond(request);
        }
    }
}
