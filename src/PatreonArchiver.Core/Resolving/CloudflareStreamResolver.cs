using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Resolving;

/// <summary>
/// Resolves a source URL to the URL yt-dlp should consume. Cloudflare Stream / iframe URLs pass
/// through; a publisher post page is fetched and its <c>iframe.videodelivery.net</c> embed is
/// extracted (semantic parse first, regex fallback) — the original <c>resolve.py</c>.
/// </summary>
internal sealed partial class CloudflareStreamResolver(HttpClient http) : IUrlResolver
{
    private const string IframePrefix = "https://iframe.videodelivery.net/";

    public async Task<Uri> ResolveAsync(Uri source, CancellationToken ct = default)
    {
        var url = source.ToString();

        if (StreamReference.TryParse(url, out _) ||
            url.StartsWith(IframePrefix, StringComparison.OrdinalIgnoreCase) ||
            CustomerStreamPattern().IsMatch(url))
        {
            return source;
        }

        var html = await http.GetStringAsync(source, ct).ConfigureAwait(false);

        var embed = ExtractIframe(html);
        return embed is not null && Uri.TryCreate(embed, UriKind.Absolute, out var iframe)
            ? iframe
            : source; // hand the original URL to yt-dlp's extractors as a last resort
    }

    private static string? ExtractIframe(string html)
    {
        var fromDom = new HtmlParser().ParseDocument(html)
            .QuerySelectorAll("iframe[src]")
            .Select(e => e.GetAttribute("src"))
            .FirstOrDefault(src => src is not null && src.StartsWith(IframePrefix, StringComparison.OrdinalIgnoreCase));

        if (fromDom is not null)
        {
            return fromDom;
        }

        var match = IframePattern().Match(html);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"^https://customer-[^./]+\.cloudflarestream\.com/", RegexOptions.CultureInvariant)]
    private static partial Regex CustomerStreamPattern();

    [GeneratedRegex(@"https://iframe\.videodelivery\.net/[A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex IframePattern();
}
