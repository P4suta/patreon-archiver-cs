namespace PatreonArchiver.Core.Domain;

/// <summary>A request to sync a creator from a parsed page snapshot.</summary>
public sealed record SyncRequest
{
    /// <summary>Default minimum free space below which downloads are halted (20 GiB).</summary>
    public const long DefaultMinimumFreeSpaceBytes = 20L * 1024 * 1024 * 1024;

    public required string Handle { get; init; }
    public required InventoryResult Inventory { get; init; }

    public string? StreamHost { get; init; }
    public string? PatreonUrl { get; init; }
    public PresetKind Preset { get; init; } = PresetKind.Polite;

    /// <summary>Preview only: parse, filter and evaluate coverage without writing state or downloading.</summary>
    public bool DryRun { get; init; }

    public string? CookiesFilePath { get; init; }

    /// <summary>Stop starting new downloads once free disk space falls below this many bytes.</summary>
    public long MinimumFreeSpaceBytes { get; init; } = DefaultMinimumFreeSpaceBytes;
}

/// <summary>Incremental progress reported during a sync.</summary>
public sealed record SyncProgress(
    int Completed,
    int Total,
    Post? Current,
    DownloadProgress? Download);

/// <summary>The aggregate outcome of a sync run.</summary>
public sealed record SyncResult(
    int Discovered,
    int New,
    int Downloaded,
    int Skipped,
    int Failed,
    CoverageAnchor Anchor,
    bool GapPending,
    string? GapWarning,
    IReadOnlyList<DownloadResult> Results)
{
    /// <summary>Set when the batch halted because free disk space dropped below the threshold.</summary>
    public DiskSpaceStatus? DiskStop { get; init; }

    public bool StoppedForDiskSpace => DiskStop is not null;
}
