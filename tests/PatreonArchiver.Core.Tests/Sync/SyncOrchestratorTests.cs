using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;
using PatreonArchiver.Core.DependencyInjection;
using PatreonArchiver.Core.Sync;

namespace PatreonArchiver.Core.Tests.Sync;

/// <summary>
/// Exercises the sync algorithm end to end against a real SQLite repository (temp database) with a
/// fake resolver/engine/throttle, validating the coverage-anchor and download-state transitions.
/// </summary>
public sealed class SyncOrchestratorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pa-sync-{Guid.NewGuid():N}.db");
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _provider;
    private readonly IArchiveRepository _repo;

    public SyncOrchestratorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(_clock);
        services.AddPatreonArchiverCore(o =>
        {
            o.DatabasePath = _dbPath;
            o.StagingRoot = Path.Combine(Path.GetTempPath(), "pa-sync-stage");
            o.OutputRoot = Path.Combine(Path.GetTempPath(), "pa-sync-out");
        });
        _provider = services.BuildServiceProvider();
        _repo = _provider.GetRequiredService<IArchiveRepository>();
        _repo.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { File.Delete(path); } catch (IOException) { /* best effort */ }
        }
    }

    [Fact]
    public async Task First_sync_downloads_all_posts_and_advances_the_anchor()
    {
        var result = await NewOrchestrator(Always(Success)).SyncAsync(
            Request(Inventory(("20260101", "a"), ("20260110", "b"))));

        Assert.Equal(2, result.New);
        Assert.Equal(2, result.Downloaded);
        Assert.False(result.GapPending);

        var creator = await _repo.FindCreatorAsync("acme");
        Assert.Equal(new DateOnly(2026, 1, 10), (await _repo.GetAnchorAsync(creator!.Id)).AnchorDate);
        Assert.All(await _repo.GetPostsAsync(creator.Id), p => Assert.Equal(PostStatus.Published, p.Status));
    }

    [Fact]
    public async Task Re_syncing_the_same_snapshot_downloads_nothing()
    {
        var engine = Always(Success);
        var orchestrator = NewOrchestrator(engine);
        var request = Request(Inventory(("20260101", "a"), ("20260110", "b")));

        await orchestrator.SyncAsync(request);
        var second = await orchestrator.SyncAsync(request);

        Assert.Equal(0, second.New);
        Assert.Equal(0, second.Downloaded);
        Assert.Equal(2, engine.Calls); // only the first run downloaded
    }

    [Fact]
    public async Task A_gap_holds_the_anchor_but_still_downloads_visible_posts()
    {
        var orchestrator = NewOrchestrator(Always(Success));
        await orchestrator.SyncAsync(Request(Inventory(("20260201", "a"), ("20260210", "b"))));

        var gap = await orchestrator.SyncAsync(Request(Inventory(("20260305", "c"), ("20260310", "d"))));

        Assert.True(gap.GapPending);
        Assert.Equal(2, gap.Downloaded);

        var creator = await _repo.FindCreatorAsync("acme");
        var anchor = await _repo.GetAnchorAsync(creator!.Id);
        Assert.Equal(new DateOnly(2026, 2, 10), anchor.AnchorDate); // held, not advanced
        Assert.True(anchor.HasGap);
    }

    [Fact]
    public async Task A_failure_keeps_the_post_eligible_and_blocks_anchor_advance()
    {
        var engine = Always(job => job.Metadata.Title == "b" ? Failure(job) : Success(job));

        var result = await NewOrchestrator(engine).SyncAsync(
            Request(Inventory(("20260101", "a"), ("20260110", "b"))));

        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, result.Failed);

        var creator = await _repo.FindCreatorAsync("acme");
        Assert.Null((await _repo.GetAnchorAsync(creator!.Id)).AnchorDate); // not advanced
        Assert.Contains(await _repo.GetDownloadablePostsAsync(creator.Id), p => p.Title == "b"); // retryable
    }

    [Fact]
    public async Task A_dry_run_has_no_side_effects()
    {
        var engine = Always(Success);

        var result = await NewOrchestrator(engine).SyncAsync(
            Request(Inventory(("20260101", "a"), ("20260110", "b")), dryRun: true));

        Assert.Equal(2, result.New);
        Assert.Equal(0, result.Downloaded);
        Assert.Equal(0, engine.Calls);
        Assert.Null(await _repo.FindCreatorAsync("acme")); // creator was never created
    }

    [Fact]
    public async Task Stops_before_downloading_when_disk_space_is_low()
    {
        var engine = Always(Success);
        // Allow the first download, then report low disk before the second.
        var orchestrator = NewOrchestrator(engine, new CountingGuard(allowed: 1));

        var result = await orchestrator.SyncAsync(Request(Inventory(("20260101", "a"), ("20260110", "b"))));

        Assert.True(result.StoppedForDiskSpace);
        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, engine.Calls);

        var creator = await _repo.FindCreatorAsync("acme");
        // The undownloaded post stays eligible (not marked failed) and the anchor did not advance.
        Assert.Contains(await _repo.GetDownloadablePostsAsync(creator!.Id), p => p.Token == "20260110_b_tok");
        Assert.Null((await _repo.GetAnchorAsync(creator.Id)).AnchorDate);
    }

    private SyncOrchestrator NewOrchestrator(ScriptedEngine engine, IDiskSpaceGuard? guard = null) =>
        new(_repo, new PassthroughResolver(), engine, new NoThrottle(), guard ?? new AlwaysFreeGuard(), _clock);

    private static SyncRequest Request(InventoryResult inventory, bool dryRun = false) =>
        new() { Handle = "acme", StreamHost = "stream.acme.tv", Inventory = inventory, DryRun = dryRun };

    private static InventoryResult Inventory(params (string Ymd, string Slug)[] items) =>
        new(items.Select(i => MakePost(i.Ymd, i.Slug)).ToList());

    private static Post MakePost(string yyyymmdd, string slug)
    {
        Assert.True(StreamReference.TryParse($"https://stream.acme.tv/{yyyymmdd}_{slug}_tok/", out var stream));
        return new Post { Id = 0, CreatorId = 0, Stream = stream, Title = slug, Status = PostStatus.Discovered };
    }

    private static ScriptedEngine Always(Func<DownloadJob, DownloadResult> respond) => new(respond);

    private static DownloadResult Success(DownloadJob job) =>
        new(job, DownloadOutcome.Success, $@"C:\lib\{job.PostId}.mp4", $"vid{job.PostId}", "generic", 0, null, TimeSpan.Zero);

    private static DownloadResult Failure(DownloadJob job) =>
        new(job, DownloadOutcome.Failed, null, null, null, 1, "boom", TimeSpan.Zero);

    private sealed class ScriptedEngine(Func<DownloadJob, DownloadResult> respond) : IDownloadEngine
    {
        public int Calls { get; private set; }

        public Task<DownloadResult> DownloadAsync(
            DownloadJob job, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(respond(job));
        }
    }

    private sealed class PassthroughResolver : IUrlResolver
    {
        public Task<Uri> ResolveAsync(Uri source, CancellationToken ct = default) => Task.FromResult(source);
    }

    private sealed class NoThrottle : IBatchThrottle
    {
        public Task WaitAsync(PresetKind preset, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AlwaysFreeGuard : IDiskSpaceGuard
    {
        public DiskSpaceStatus Check(long minimumFreeBytes) => new(true, long.MaxValue, minimumFreeBytes);
    }

    private sealed class CountingGuard(int allowed) : IDiskSpaceGuard
    {
        private int _checks;

        public DiskSpaceStatus Check(long minimumFreeBytes)
        {
            var hasHeadroom = _checks++ < allowed;
            return new DiskSpaceStatus(hasHeadroom, hasHeadroom ? long.MaxValue : 1_000_000_000, minimumFreeBytes);
        }
    }
}
