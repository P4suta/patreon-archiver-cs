using System.Globalization;
using System.Text.RegularExpressions;

namespace PatreonArchiver.Core.Domain;

/// <summary>
/// A Cloudflare Stream video URL of the form
/// <c>https://stream.&lt;host&gt;/{yyyymmdd}_{slug}_{token}/</c>, parsed into its parts.
/// The <see cref="Segment"/> is the natural per-creator identity (replaces the original
/// tool's <c>seen_posts.txt</c> entries).
/// </summary>
public sealed partial record StreamReference
{
    private StreamReference(Uri url, string host, DateOnly date, string slug, string token)
    {
        Url = url;
        Host = host;
        Date = date;
        Slug = slug;
        Token = token;
    }

    /// <summary>The full, absolute stream URL.</summary>
    public Uri Url { get; }

    /// <summary>The streaming host, e.g. <c>stream.example.com</c>.</summary>
    public string Host { get; }

    /// <summary>The publish date encoded in the 8-digit URL prefix.</summary>
    public DateOnly Date { get; }

    /// <summary>The URL slug between the date and the token.</summary>
    public string Slug { get; }

    /// <summary>The opaque access token tail of the URL.</summary>
    public string Token { get; }

    /// <summary>The "{yyyymmdd}_{slug}_{token}" path segment — unique per creator.</summary>
    public string Segment => $"{Date:yyyyMMdd}_{Slug}_{Token}";

    [GeneratedRegex(
        @"^(?<scheme>https?)://(?<host>stream\.[^/]+)/(?<date>\d{8})_(?<slug>[^_/]+)_(?<token>[^/]+)/?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex(
        @"https?://stream\.[^/\s""'<>]+/\d{8}_[^/\s""'<>]+/?",
        RegexOptions.CultureInvariant)]
    private static partial Regex ScanPattern();

    /// <summary>Finds and validates every stream URL embedded in an HTML/text blob.</summary>
    public static IEnumerable<StreamReference> Scan(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match match in ScanPattern().Matches(text))
        {
            if (TryParse(match.Value, out var reference))
            {
                yield return reference;
            }
        }
    }

    /// <summary>
    /// Parses a Cloudflare Stream URL. Rejects malformed dates, mirroring the original
    /// <c>date.fromisoformat</c> validation gate.
    /// </summary>
    public static bool TryParse(string? candidate, out StreamReference reference)
    {
        reference = null!;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var match = Pattern().Match(candidate.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!DateOnly.TryParseExact(
                match.Groups["date"].Value, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var url))
        {
            return false;
        }

        reference = new StreamReference(
            url, match.Groups["host"].Value, date,
            match.Groups["slug"].Value, match.Groups["token"].Value);
        return true;
    }
}
