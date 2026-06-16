using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Configuration;

/// <summary>Host-supplied configuration for the Core services. Bound via <c>AddPatreonArchiverCore</c>.</summary>
public sealed class CoreOptions
{
    /// <summary>SQLite database file (e.g. <c>%LOCALAPPDATA%\PatreonArchiver\state.db</c>).</summary>
    public required string DatabasePath { get; set; }

    /// <summary>Scratch directory for in-flight downloads, swept on publish.</summary>
    public required string StagingRoot { get; set; }

    /// <summary>Library root; published files land under <c>&lt;OutputRoot&gt;/&lt;uploader&gt;/...</c>.</summary>
    public required string OutputRoot { get; set; }

    /// <summary>Optional Netscape cookies file for yt-dlp fallback auth.</summary>
    public string? CookiesFilePath { get; set; }

    /// <summary>Directory holding the bundled yt-dlp/ffmpeg; defaults to <c>&lt;app&gt;/tools</c>.</summary>
    public string? ToolsDirectory { get; set; }

    /// <summary>Timezone used for yt-dlp date derivation (the original compose default was Asia/Tokyo).</summary>
    public string TimeZoneId { get; set; } = "Asia/Tokyo";

    public PresetKind DefaultPreset { get; set; } = PresetKind.Polite;
}
