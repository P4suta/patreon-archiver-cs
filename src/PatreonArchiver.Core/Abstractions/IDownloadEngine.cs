using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>Downloads a single <see cref="DownloadJob"/> via the bundled yt-dlp/ffmpeg and publishes the result.</summary>
public interface IDownloadEngine
{
    Task<DownloadResult> DownloadAsync(
        DownloadJob job,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);
}
