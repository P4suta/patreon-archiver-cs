using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Sync;

/// <summary>Paces successive downloads (the original <c>YTDLP_BATCH_SLEEP_MIN/MAX</c>).</summary>
internal interface IBatchThrottle
{
    Task WaitAsync(PresetKind preset, CancellationToken ct = default);
}
