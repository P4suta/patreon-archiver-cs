using PatreonArchiver.Core.Domain;
using PatreonArchiver.Core.Parsing;

namespace PatreonArchiver.Core.Tests.Parsing;

public sealed class AngleSharpPostPageParserTests
{
    private readonly AngleSharpPostPageParser _parser =
        new(new TestClock(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Extracts_posts_from_cards_with_title_and_post_link()
    {
        const string html = """
            <div data-tag="post-card">
              <a href="/posts/intro-123">open</a>
              <span data-tag="post-title">Intro Episode</span>
              <iframe src="https://stream.acme.tv/20260101_intro_tok1/"></iframe>
            </div>
            <div data-tag="post-card">
              <a href="/posts/ep1-124">open</a>
              <iframe src="https://stream.acme.tv/20260115_ep-1_tok2/"></iframe>
            </div>
            """;

        var result = _parser.Parse(html);

        Assert.Equal(2, result.VideoPostCount);
        Assert.Equal(new DateOnly(2026, 1, 1), result.OldestDate);
        Assert.Equal(new DateOnly(2026, 1, 15), result.NewestDate);

        var intro = result.Posts[0];
        Assert.Equal("Intro Episode", intro.Title);
        Assert.Contains("/posts/intro-123", intro.PatreonPostUrl);
        Assert.Equal("20260101_intro_tok1", intro.Token);
        Assert.Equal(PostStatus.Discovered, intro.Status);

        Assert.Equal("Ep 1", result.Posts[1].Title); // humanized from slug when no title element
    }

    [Fact]
    public void Deduplicates_repeated_stream_urls()
    {
        const string html = """
            <div data-tag="post-card"><iframe src="https://stream.acme.tv/20260101_x_tok/"></iframe></div>
            <div data-tag="post-card"><iframe src="https://stream.acme.tv/20260101_x_tok/"></iframe></div>
            """;

        Assert.Equal(1, _parser.Parse(html).VideoPostCount);
    }

    [Fact]
    public void Falls_back_to_scanning_when_there_are_no_post_cards()
    {
        const string html = """
            <html><body>
              <script>var src = "https://stream.acme.tv/20260201_loose_tok/";</script>
            </body></html>
            """;

        var result = _parser.Parse(html);

        Assert.Equal(1, result.VideoPostCount);
        Assert.Equal(new DateOnly(2026, 2, 1), result.Posts[0].Date);
    }

    [Fact]
    public void Ignores_cards_without_a_valid_stream_url()
    {
        const string html = """
            <div data-tag="post-card"><a href="/posts/text-only">just text</a></div>
            """;

        Assert.Equal(0, _parser.Parse(html).VideoPostCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_yields_empty_inventory(string html)
    {
        Assert.Same(InventoryResult.Empty, _parser.Parse(html));
    }
}
