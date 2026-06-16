using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>Orchestrates a full sync: filter new posts, evaluate coverage, download, advance state.</summary>
public interface ISyncOrchestrator
{
    Task<SyncResult> SyncAsync(
        SyncRequest request,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default);
}
