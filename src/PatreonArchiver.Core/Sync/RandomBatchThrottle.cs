using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Sync;

/// <summary>Waits a random duration within the preset's batch-sleep window between downloads.</summary>
internal sealed class RandomBatchThrottle : IBatchThrottle
{
    public Task WaitAsync(PresetKind preset, CancellationToken ct = default)
    {
        var window = DownloadPreset.For(preset);
        if (window.BatchSleepMaxMs <= 0)
        {
            return Task.CompletedTask;
        }

        var milliseconds = Random.Shared.Next(window.BatchSleepMinMs, window.BatchSleepMaxMs + 1);
        return Task.Delay(milliseconds, ct);
    }
}
