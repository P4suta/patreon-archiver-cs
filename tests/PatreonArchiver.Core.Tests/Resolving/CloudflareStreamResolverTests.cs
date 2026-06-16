using System.Net;
using PatreonArchiver.Core.Resolving;

namespace PatreonArchiver.Core.Tests.Resolving;

public sealed class CloudflareStreamResolverTests
{
    [Fact]
    public async Task Passes_through_a_stream_url_without_any_http_call()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not fetch"));
        var resolver = new CloudflareStreamResolver(new HttpClient(handler));

        var source = new Uri("https://stream.acme.tv/20260101_intro_tok/");
        var resolved = await resolver.ResolveAsync(source);

        Assert.Equal(source, resolved);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Passes_through_an_iframe_url_without_any_http_call()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not fetch"));
        var resolver = new CloudflareStreamResolver(new HttpClient(handler));

        var source = new Uri("https://iframe.videodelivery.net/abc123");
        Assert.Equal(source, await resolver.ResolveAsync(source));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Extracts_the_iframe_from_a_fetched_post_page()
    {
        var handler = new StubHandler(_ => Html(
            """<html><body><iframe src="https://iframe.videodelivery.net/VIDEO42"></iframe></body></html>"""));
        var resolver = new CloudflareStreamResolver(new HttpClient(handler));

        var resolved = await resolver.ResolveAsync(new Uri("https://www.patreon.com/posts/abc-1"));

        Assert.Equal("https://iframe.videodelivery.net/VIDEO42", resolved.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Falls_back_to_regex_when_the_iframe_is_script_injected()
    {
        var handler = new StubHandler(_ => Html(
            """<html><head><script>player("https://iframe.videodelivery.net/inj99")</script></head></html>"""));
        var resolver = new CloudflareStreamResolver(new HttpClient(handler));

        var resolved = await resolver.ResolveAsync(new Uri("https://www.patreon.com/posts/abc-2"));

        Assert.Equal("https://iframe.videodelivery.net/inj99", resolved.ToString());
    }

    private static HttpResponseMessage Html(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responder(request));
        }
    }
}
