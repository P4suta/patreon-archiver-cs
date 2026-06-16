namespace PatreonArchiver.Core.Domain;

/// <summary>Politeness/throughput trade-off for downloads (mirrors the original polite vs fast configs).</summary>
public enum PresetKind
{
    Polite,
    Fast,
}

/// <summary>Terminal outcome of a single download attempt.</summary>
public enum DownloadOutcome
{
    Success,
    AlreadyArchived,
    Failed,
    Cancelled,
}

/// <summary>A single unit of work for the download engine.</summary>
public sealed record DownloadJob
{
    /// <summary>The yt-dlp-consumable URL (resolved iframe or passthrough stream URL).</summary>
    public required string SourceUrl { get; init; }

    public PostMetadata Metadata { get; init; } = PostMetadata.Empty;
    public PresetKind Preset { get; init; } = PresetKind.Polite;

    /// <summary><c>--simulate</c>: probe metadata without downloading.</summary>
    public bool Simulate { get; init; }

    /// <summary>Download into an isolated sandbox, bypassing dedup (the original <c>retest</c>).</summary>
    public bool Retest { get; init; }

    /// <summary>The originating <see cref="Post"/>, when the job came from a sync.</summary>
    public long? PostId { get; init; }

    /// <summary>Netscape cookies file for yt-dlp fallback auth, when available.</summary>
    public string? CookiesFilePath { get; init; }

    /// <summary>yt-dlp <c>--download-archive</c> file for dedup; null skips it (e.g. retest).</summary>
    public string? ArchiveFile { get; init; }
}

/// <summary>Progress of a download, parsed from yt-dlp's per-line output.</summary>
public readonly record struct DownloadProgress(
    double Percent,
    string? TotalSize,
    string? Speed,
    string? Eta);

/// <summary>The result of a single download attempt.</summary>
public sealed record DownloadResult(
    DownloadJob Job,
    DownloadOutcome Outcome,
    string? PublishedPath,
    string? VideoId,
    string? Extractor,
    int ExitCode,
    string? Error,
    TimeSpan Elapsed)
{
    public bool IsSuccess => Outcome is DownloadOutcome.Success;
}
