using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Configuration;

/// <summary>
/// Concrete rate-limit settings for a <see cref="PresetKind"/>, ported from the original
/// <c>yt-dlp.conf</c> / <c>yt-dlp-fast.conf</c> presets.
/// </summary>
public sealed record DownloadPreset(
    PresetKind Kind,
    int ConcurrentFragments,
    int SleepRequestsSeconds,
    int BatchSleepMinMs,
    int BatchSleepMaxMs)
{
    /// <summary>Server-friendly default: 4 fragments, 1s request spacing, 5–15s between videos.</summary>
    public static readonly DownloadPreset Polite = new(PresetKind.Polite, 4, 1, 5_000, 15_000);

    /// <summary>Aggressive: 8 fragments, no request spacing, 0–2s between videos.</summary>
    public static readonly DownloadPreset Fast = new(PresetKind.Fast, 8, 0, 0, 2_000);

    public static DownloadPreset For(PresetKind kind) =>
        kind == PresetKind.Fast ? Fast : Polite;
}
