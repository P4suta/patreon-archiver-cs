using System.Globalization;
using System.Text.RegularExpressions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Downloading;

/// <summary>Parses yt-dlp's per-line download output (emitted under <c>--newline</c>) into progress.</summary>
internal static partial class YtDlpProgressParser
{
    [GeneratedRegex(
        @"\[download\]\s+(?<pct>\d{1,3}(?:\.\d+)?)%" +
        @"(?:\s+of\s+~?\s*(?<total>[\d.]+\w+))?" +
        @"(?:\s+at\s+(?<speed>[\d.]+\w+/s))?" +
        @"(?:\s+ETA\s+(?<eta>[\d:]+))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex Line();

    public static bool TryParse(string line, out DownloadProgress progress)
    {
        progress = default;
        var match = Line().Match(line);
        if (!match.Success)
        {
            return false;
        }

        progress = new DownloadProgress(
            double.Parse(match.Groups["pct"].Value, CultureInfo.InvariantCulture),
            Value(match, "total"),
            Value(match, "speed"),
            Value(match, "eta"));
        return true;

        static string? Value(Match match, string group) =>
            match.Groups[group].Success ? match.Groups[group].Value : null;
    }
}
