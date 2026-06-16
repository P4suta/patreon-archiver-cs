using System.Globalization;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Downloading;

/// <summary>The inputs needed to assemble a yt-dlp command line.</summary>
internal readonly record struct YtDlpInvocation(
    DownloadJob Job,
    ToolPaths Tools,
    string StagingDirectory,
    string? ArchiveFile,
    string? CookiesFile);

/// <summary>
/// Builds the yt-dlp argument vector, faithfully porting the original <c>yt-dlp.conf</c> flags and
/// <c>download.py</c> metadata injection. Pure and deterministic so it can be asserted exactly.
/// </summary>
internal static class YtDlpArgBuilder
{
    /// <summary>Sentinel prefixing our <c>--print</c> line so the engine can find it in stdout.</summary>
    public const string DoneMarker = "#PADONE#";

    private const string OutputTemplate = "%(uploader)s/%(upload_date>%Y-%m-%d)s_%(title).140B.%(ext)s";

    public static IReadOnlyList<string> Build(YtDlpInvocation invocation)
    {
        var job = invocation.Job;
        var preset = DownloadPreset.For(job.Preset);

        var args = new List<string>
        {
            "--ffmpeg-location", invocation.Tools.FfmpegDirectory,
            "--paths", $"home:{invocation.StagingDirectory}",
            "--output", OutputTemplate,
            "--format", "bv*+ba/b",
            "--merge-output-format", "mp4",
            "--remux-video", "mp4",
            "--format-sort", "res,br,proto",
            "--concurrent-fragments", preset.ConcurrentFragments.ToString(CultureInfo.InvariantCulture),
            "--sleep-requests", preset.SleepRequestsSeconds.ToString(CultureInfo.InvariantCulture),
            "--retries", "10",
            "--fragment-retries", "10",
            "--retry-sleep", "fragment:exp=1:60",
            "--retry-sleep", "http:exp=1:30",
            "--retry-sleep", "extractor:exp=1:30",
            "--continue",
            "--no-overwrites",
            "--embed-metadata",
            "--embed-thumbnail",
            "--no-mtime",
            "--ignore-config",
            "--newline",
            "--print", $"after_move:{DoneMarker} %(extractor_key)s\t%(id)s\t%(filepath)s",
        };

        if (!string.IsNullOrEmpty(invocation.ArchiveFile))
        {
            args.Add("--download-archive");
            args.Add(invocation.ArchiveFile);
        }

        if (!string.IsNullOrEmpty(invocation.CookiesFile))
        {
            args.Add("--cookies");
            args.Add(invocation.CookiesFile);
        }

        args.AddRange(MetadataFlags(job.Metadata));

        if (job.Simulate)
        {
            args.Add("--simulate");
        }

        args.Add(job.SourceUrl);
        return args;
    }

    /// <summary>
    /// Ports <c>download.py</c>'s metadata injection: the <c>"= "</c> sentinel defeats yt-dlp's
    /// field auto-wrapping; literal colons in values are escaped as <c>\:</c>.
    /// </summary>
    private static IEnumerable<string> MetadataFlags(PostMetadata metadata)
    {
        if (metadata.Date is { } date)
        {
            foreach (var arg in Inject($"{date:yyyyMMdd}", "upload_date"))
            {
                yield return arg;
            }
        }

        if (!string.IsNullOrEmpty(metadata.Title))
        {
            foreach (var arg in Inject(metadata.Title, "title")) { yield return arg; }
            foreach (var arg in Inject(metadata.Title, "meta_title")) { yield return arg; }
        }

        if (!string.IsNullOrEmpty(metadata.Uploader))
        {
            foreach (var arg in Inject(metadata.Uploader, "uploader")) { yield return arg; }
        }

        if (!string.IsNullOrEmpty(metadata.PostUrl))
        {
            foreach (var arg in Inject(metadata.PostUrl, "meta_comment")) { yield return arg; }
            foreach (var arg in Inject(metadata.PostUrl, "meta_purl")) { yield return arg; }
        }

        static IEnumerable<string> Inject(string value, string field) =>
            ["--parse-metadata", $"= {Escape(value)}:= %({field})s"];

        static string Escape(string value) => value.Replace(":", @"\:", StringComparison.Ordinal);
    }
}
