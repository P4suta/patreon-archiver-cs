using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Parsing;

/// <summary>
/// Parses a creator's posts page into an inventory. Mirrors the original <c>inventory.py</c>:
/// it locates <c>data-tag="post-card"</c> elements and extracts the Cloudflare Stream URL from
/// each. The HTML may be a live WebView2 DOM snapshot or extracted MHTML — handling is identical.
/// </summary>
internal sealed class AngleSharpPostPageParser(IClock clock) : IPostPageParser
{
    public InventoryResult Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return InventoryResult.Empty;
        }

        var document = new HtmlParser().ParseDocument(html);
        var cards = document.QuerySelectorAll("[data-tag='post-card']");

        // Post cards are the source of truth; if a snapshot has none (e.g. a raw DOM fragment),
        // fall back to scanning the whole document for stream URLs.
        var posts = cards.Length > 0 ? FromCards(cards) : FromLooseScan(html);
        return new InventoryResult(posts);
    }

    private List<Post> FromCards(IHtmlCollection<IElement> cards)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var posts = new List<Post>();

        foreach (var card in cards)
        {
            var stream = StreamReference.Scan(card.OuterHtml).FirstOrDefault();
            if (stream is null || !seen.Add(stream.Segment))
            {
                continue;
            }

            posts.Add(NewPost(stream, ExtractTitle(card, stream.Slug), ExtractPostLink(card)));
        }

        return posts;
    }

    private List<Post> FromLooseScan(string html)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var posts = new List<Post>();

        foreach (var stream in StreamReference.Scan(html))
        {
            if (seen.Add(stream.Segment))
            {
                posts.Add(NewPost(stream, Humanize(stream.Slug), postUrl: null));
            }
        }

        return posts;
    }

    private Post NewPost(StreamReference stream, string? title, string? postUrl) => new()
    {
        Id = 0,
        CreatorId = 0,
        Stream = stream,
        Title = title,
        PatreonPostUrl = postUrl,
        Status = PostStatus.Discovered,
        DiscoveredAt = clock.UtcNow,
    };

    private static string ExtractTitle(IElement card, string slug)
    {
        var text = card.QuerySelector("[data-tag='post-title']")?.TextContent?.Trim();
        return string.IsNullOrWhiteSpace(text) ? Humanize(slug) : text;
    }

    private static string? ExtractPostLink(IElement card) =>
        card.QuerySelectorAll("a[href]")
            .Select(a => a.GetAttribute("href"))
            .FirstOrDefault(href => href is not null && href.Contains("/posts/", StringComparison.OrdinalIgnoreCase));

    private static string Humanize(string slug)
    {
        var spaced = slug.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(spaced) ? slug : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }
}
