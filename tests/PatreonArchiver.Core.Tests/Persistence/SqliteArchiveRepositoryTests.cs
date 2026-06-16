using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.DependencyInjection;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Tests.Persistence;

/// <summary>
/// Exercises the SQLite repository through its public <see cref="IArchiveRepository"/> surface
/// (composed via DI), against a throwaway file database.
/// </summary>
public sealed class SqliteArchiveRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pa-test-{Guid.NewGuid():N}.db");
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _provider;
    private readonly IArchiveRepository _repo;

    public SqliteArchiveRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(_clock);
        services.AddPatreonArchiverCore(o =>
        {
            o.DatabasePath = _dbPath;
            o.StagingRoot = Path.Combine(Path.GetTempPath(), "pa-staging");
            o.OutputRoot = Path.Combine(Path.GetTempPath(), "pa-out");
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
    public async Task Initialize_is_idempotent()
    {
        await _repo.InitializeAsync();
        await _repo.InitializeAsync(); // must not throw on an already-migrated database
    }

    [Fact]
    public async Task GetOrCreateCreator_is_stable_and_fills_in_host()
    {
        var first = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        var second = await _repo.GetOrCreateCreatorAsync("acme", "stream.acme.tv", "https://patreon.com/acme");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("stream.acme.tv", second.StreamHost);
        Assert.Equal("https://patreon.com/acme", second.PatreonUrl);
    }

    [Fact]
    public async Task Upsert_inserts_new_posts_and_assigns_ids()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        var saved = await _repo.UpsertPostsAsync(creator.Id,
        [
            MakePost(creator.Id, "20260101", "intro"),
            MakePost(creator.Id, "20260102", "ep-1"),
        ]);

        Assert.Equal(2, saved.Count);
        Assert.All(saved, p => Assert.True(p.Id > 0));
        Assert.All(saved, p => Assert.Equal(PostStatus.Discovered, p.Status));
    }

    [Fact]
    public async Task Upsert_preserves_download_state_but_refreshes_title()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        var saved = await _repo.UpsertPostsAsync(creator.Id, [MakePost(creator.Id, "20260101", "intro", title: "Intro")]);
        await _repo.MarkPostAsync(saved[0].Id, PostStatus.Published, filePath: @"C:\lib\acme\intro.mp4", videoId: "vid1");

        // Re-discovering the same token (e.g. on the next sync) must not clobber the published state.
        await _repo.UpsertPostsAsync(creator.Id, [MakePost(creator.Id, "20260101", "intro", title: "Intro (updated)")]);

        var post = (await _repo.GetPostsAsync(creator.Id)).Single();
        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(@"C:\lib\acme\intro.mp4", post.FilePath);
        Assert.Equal("vid1", post.VideoId);
        Assert.Equal("Intro (updated)", post.Title);
    }

    [Fact]
    public async Task GetKnownTokens_returns_every_stored_token()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        await _repo.UpsertPostsAsync(creator.Id,
        [
            MakePost(creator.Id, "20260101", "a"),
            MakePost(creator.Id, "20260102", "b"),
        ]);

        var tokens = await _repo.GetKnownTokensAsync(creator.Id);

        Assert.Equal(2, tokens.Count);
        Assert.Contains("20260101_a_tok", tokens);
        Assert.Contains("20260102_b_tok", tokens);
    }

    [Fact]
    public async Task GetDownloadablePosts_returns_only_discovered_and_failed_oldest_first()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        await _repo.UpsertPostsAsync(creator.Id,
        [
            MakePost(creator.Id, "20260104", "failed", PostStatus.Failed),
            MakePost(creator.Id, "20260101", "discovered", PostStatus.Discovered),
            MakePost(creator.Id, "20260102", "published", PostStatus.Published),
            MakePost(creator.Id, "20260103", "skipped", PostStatus.Skipped),
        ]);

        var downloadable = await _repo.GetDownloadablePostsAsync(creator.Id);

        Assert.Equal(2, downloadable.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), downloadable[0].Date); // oldest first
        Assert.Equal(new DateOnly(2026, 1, 4), downloadable[1].Date);
        Assert.All(downloadable, p => Assert.True(p.Status is PostStatus.Discovered or PostStatus.Failed));
    }

    [Fact]
    public async Task MarkPost_published_records_path_and_timestamp()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        var saved = await _repo.UpsertPostsAsync(creator.Id, [MakePost(creator.Id, "20260101", "intro")]);

        await _repo.MarkPostAsync(saved[0].Id, PostStatus.Published, @"C:\lib\acme\intro.mp4", "vid42");

        var post = (await _repo.GetPostsAsync(creator.Id)).Single();
        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal("vid42", post.VideoId);
        Assert.Equal(_clock.UtcNow, post.PublishedAt);
    }

    [Fact]
    public async Task Anchor_defaults_to_none()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        var anchor = await _repo.GetAnchorAsync(creator.Id);

        Assert.False(anchor.HasGap);
        Assert.Null(anchor.AnchorDate);
    }

    [Fact]
    public async Task AdvanceAnchor_sets_date_and_clears_any_pending_gap()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        await _repo.SetGapAsync(creator.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15));

        await _repo.AdvanceAnchorAsync(creator.Id, new DateOnly(2026, 3, 1));

        var anchor = await _repo.GetAnchorAsync(creator.Id);
        Assert.Equal(new DateOnly(2026, 3, 1), anchor.AnchorDate);
        Assert.False(anchor.HasGap);
    }

    [Fact]
    public async Task SetGap_keeps_the_earliest_pending_from()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        await _repo.AdvanceAnchorAsync(creator.Id, new DateOnly(2026, 3, 1));

        await _repo.SetGapAsync(creator.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15));
        await _repo.SetGapAsync(creator.Id, new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 20));

        var anchor = await _repo.GetAnchorAsync(creator.Id);
        Assert.True(anchor.HasGap);
        Assert.Equal(new DateOnly(2026, 2, 1), anchor.PendingGapFrom);   // earliest wins
        Assert.Equal(new DateOnly(2026, 3, 1), anchor.AnchorDate);       // anchor untouched by SetGap
    }

    [Fact]
    public async Task Archive_records_dedup_and_reports_membership()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);

        Assert.False(await _repo.IsArchivedAsync(creator.Id, "generic", "vidX"));

        await _repo.RecordArchiveAsync(creator.Id, "generic", "vidX");
        await _repo.RecordArchiveAsync(creator.Id, "generic", "vidX"); // idempotent

        Assert.True(await _repo.IsArchivedAsync(creator.Id, "generic", "vidX"));
    }

    [Fact]
    public async Task ExportArchiveFile_writes_extractor_id_lines()
    {
        var creator = await _repo.GetOrCreateCreatorAsync("acme", null, null);
        await _repo.RecordArchiveAsync(creator.Id, "generic", "a1");
        await _repo.RecordArchiveAsync(creator.Id, "cloudflarestream", "b2");

        var path = await _repo.ExportArchiveFileAsync(creator.Id);

        var lines = await File.ReadAllLinesAsync(path);
        Assert.Contains("generic a1", lines);
        Assert.Contains("cloudflarestream b2", lines);
    }

    private static Post MakePost(
        long creatorId, string yyyymmdd, string slug,
        PostStatus status = PostStatus.Discovered, string? title = null)
    {
        var url = $"https://stream.acme.tv/{yyyymmdd}_{slug}_tok/";
        Assert.True(StreamReference.TryParse(url, out var stream));
        return new Post
        {
            Id = 0,
            CreatorId = creatorId,
            Stream = stream,
            Title = title ?? slug,
            Status = status,
            DiscoveredAt = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero),
        };
    }
}
