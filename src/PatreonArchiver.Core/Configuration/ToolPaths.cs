namespace PatreonArchiver.Core.Configuration;

/// <summary>Resolved filesystem locations of the bundled external tools.</summary>
public sealed record ToolPaths(string YtDlp, string Ffmpeg, string? Ffprobe)
{
    /// <summary>The directory containing ffmpeg, passed to yt-dlp via <c>--ffmpeg-location</c>.</summary>
    public string FfmpegDirectory => Path.GetDirectoryName(Ffmpeg) ?? ".";
}
