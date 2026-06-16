using Dapper;
using Microsoft.Data.Sqlite;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Persistence;

/// <summary>
/// SQLite-backed <see cref="IArchiveRepository"/>. WAL allows concurrent readers; a single
/// <see cref="SemaphoreSlim"/> serializes writers. All blocking ADO calls run on the thread
/// pool (Microsoft.Data.Sqlite has no real async I/O) so the UI thread is never blocked.
/// </summary>
internal sealed class SqliteArchiveRepository : IArchiveRepository, IDisposable
{
    static SqliteArchiveRepository() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    private readonly SqliteConnectionFactory _factory;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public SqliteArchiveRepository(SqliteConnectionFactory factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Task.Run(() => new MigrationRunner(_factory).Apply(), ct).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public Task<Creator> GetOrCreateCreatorAsync(
        string handle, string? streamHost, string? patreonUrl, CancellationToken ct = default) =>
        WriteAsync(c => c.QuerySingle<CreatorRow>(
            """
            INSERT INTO creators(handle, display_name, stream_host, patreon_url)
            VALUES (@handle, NULL, @streamHost, @patreonUrl)
            ON CONFLICT(handle) DO UPDATE SET
                stream_host = COALESCE(excluded.stream_host, creators.stream_host),
                patreon_url = COALESCE(excluded.patreon_url, creators.patreon_url)
            RETURNING id, handle, display_name, stream_host, patreon_url;
            """,
            new { handle, streamHost, patreonUrl }).ToDomain(), ct);

    public Task<Creator?> FindCreatorAsync(string handle, CancellationToken ct = default) =>
        ReadAsync(c => c.QuerySingleOrDefault<CreatorRow>(
            "SELECT id, handle, display_name, stream_host, patreon_url FROM creators WHERE handle = @handle;",
            new { handle })?.ToDomain(), ct);

    public Task<IReadOnlyList<Creator>> GetCreatorsAsync(CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<Creator>>(c => c.Query<CreatorRow>(
            "SELECT id, handle, display_name, stream_host, patreon_url FROM creators ORDER BY handle;")
            .Select(r => r.ToDomain()).ToList(), ct);

    public Task<IReadOnlySet<string>> GetKnownTokensAsync(long creatorId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlySet<string>>(c =>
            c.Query<string>("SELECT token FROM posts WHERE creator_id = @creatorId;", new { creatorId })
                .ToHashSet(StringComparer.Ordinal), ct);

    public Task<IReadOnlyList<Post>> UpsertPostsAsync(
        long creatorId, IEnumerable<Post> posts, CancellationToken ct = default) =>
        WriteAsync<IReadOnlyList<Post>>(c =>
        {
            var list = posts.ToList();
            if (list.Count == 0)
            {
                return [];
            }

            using var tx = c.BeginTransaction();
            foreach (var post in list)
            {
                c.Execute(
                    """
                    INSERT INTO posts(creator_id, token, stream_url, post_date, slug, title,
                                      resolved_iframe, patreon_post_url, video_id, status, file_path,
                                      discovered_at, published_at)
                    VALUES (@creatorId, @token, @streamUrl, @postDate, @slug, @title,
                            @resolvedIframe, @patreonPostUrl, @videoId, @status, @filePath,
                            @discoveredAt, @publishedAt)
                    ON CONFLICT(creator_id, token) DO UPDATE SET
                        stream_url       = excluded.stream_url,
                        title            = COALESCE(excluded.title, posts.title),
                        resolved_iframe  = COALESCE(excluded.resolved_iframe, posts.resolved_iframe),
                        patreon_post_url = COALESCE(excluded.patreon_post_url, posts.patreon_post_url);
                    """,
                    new
                    {
                        creatorId,
                        token = post.Token,
                        streamUrl = post.Stream.Url.ToString(),
                        postDate = Conv.Iso(post.Date),
                        slug = post.Stream.Slug,
                        title = post.Title,
                        resolvedIframe = post.ResolvedIframe?.ToString(),
                        patreonPostUrl = post.PatreonPostUrl,
                        videoId = post.VideoId,
                        status = (int)post.Status,
                        filePath = post.FilePath,
                        discoveredAt = Conv.Iso(post.DiscoveredAt == default ? _clock.UtcNow : post.DiscoveredAt),
                        publishedAt = post.PublishedAt is { } p ? Conv.Iso(p) : null,
                    },
                    tx);
            }

            var tokens = list.Select(p => p.Token).ToArray();
            var rows = c.Query<PostRow>(
                "SELECT * FROM posts WHERE creator_id = @creatorId AND token IN @tokens ORDER BY post_date ASC;",
                new { creatorId, tokens }, tx).ToList();
            tx.Commit();
            return rows.Select(r => r.ToDomain()).ToList();
        }, ct);

    public Task<IReadOnlyList<Post>> GetDownloadablePostsAsync(long creatorId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<Post>>(c => c.Query<PostRow>(
            "SELECT * FROM posts WHERE creator_id = @creatorId AND status IN (@discovered, @failed) ORDER BY post_date ASC;",
            new { creatorId, discovered = (int)PostStatus.Discovered, failed = (int)PostStatus.Failed })
            .Select(r => r.ToDomain()).ToList(), ct);

    public Task<IReadOnlyList<Post>> GetPostsAsync(long creatorId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<Post>>(c => c.Query<PostRow>(
            "SELECT * FROM posts WHERE creator_id = @creatorId ORDER BY post_date DESC, id DESC;",
            new { creatorId })
            .Select(r => r.ToDomain()).ToList(), ct);

    public Task MarkPostAsync(
        long postId, PostStatus status, string? filePath = null, string? videoId = null, CancellationToken ct = default) =>
        WriteAsync(c => c.Execute(
            """
            UPDATE posts SET
                status       = @status,
                file_path    = COALESCE(@filePath, file_path),
                video_id     = COALESCE(@videoId, video_id),
                published_at = COALESCE(@publishedAt, published_at)
            WHERE id = @postId;
            """,
            new
            {
                postId,
                status = (int)status,
                filePath,
                videoId,
                publishedAt = status == PostStatus.Published ? Conv.Iso(_clock.UtcNow) : null,
            }), ct);

    public Task<CoverageAnchor> GetAnchorAsync(long creatorId, CancellationToken ct = default) =>
        ReadAsync(c =>
            c.QuerySingleOrDefault<AnchorRow>(
                "SELECT creator_id, anchor_date, pending_gap_from, pending_gap_to, updated_at FROM coverage_anchors WHERE creator_id = @creatorId;",
                new { creatorId })?.ToDomain()
            ?? CoverageAnchor.None(creatorId, _clock.UtcNow), ct);

    public Task AdvanceAnchorAsync(long creatorId, DateOnly anchor, CancellationToken ct = default) =>
        WriteAsync(c => c.Execute(
            """
            INSERT INTO coverage_anchors(creator_id, anchor_date, pending_gap_from, pending_gap_to, updated_at)
            VALUES (@creatorId, @anchor, NULL, NULL, @now)
            ON CONFLICT(creator_id) DO UPDATE SET
                anchor_date      = excluded.anchor_date,
                pending_gap_from = NULL,
                pending_gap_to   = NULL,
                updated_at       = excluded.updated_at;
            """,
            new { creatorId, anchor = Conv.Iso(anchor), now = Conv.Iso(_clock.UtcNow) }), ct);

    public Task SetGapAsync(long creatorId, DateOnly gapFrom, DateOnly gapTo, CancellationToken ct = default) =>
        WriteAsync(c => c.Execute(
            """
            INSERT INTO coverage_anchors(creator_id, anchor_date, pending_gap_from, pending_gap_to, updated_at)
            VALUES (@creatorId, NULL, @gapFrom, @gapTo, @now)
            ON CONFLICT(creator_id) DO UPDATE SET
                pending_gap_from = MIN(COALESCE(coverage_anchors.pending_gap_from, @gapFrom), @gapFrom),
                pending_gap_to   = @gapTo,
                updated_at       = @now;
            """,
            new { creatorId, gapFrom = Conv.Iso(gapFrom), gapTo = Conv.Iso(gapTo), now = Conv.Iso(_clock.UtcNow) }), ct);

    public Task<bool> IsArchivedAsync(long creatorId, string extractor, string videoId, CancellationToken ct = default) =>
        ReadAsync(c => c.ExecuteScalar<long>(
            "SELECT EXISTS(SELECT 1 FROM download_archive WHERE creator_id = @creatorId AND extractor = @extractor AND video_id = @videoId);",
            new { creatorId, extractor, videoId }) != 0, ct);

    public Task RecordArchiveAsync(long creatorId, string extractor, string videoId, CancellationToken ct = default) =>
        WriteAsync(c => c.Execute(
            "INSERT OR IGNORE INTO download_archive(creator_id, extractor, video_id, archived_at) VALUES (@creatorId, @extractor, @videoId, @now);",
            new { creatorId, extractor, videoId, now = Conv.Iso(_clock.UtcNow) }), ct);

    public Task<string> ExportArchiveFileAsync(long creatorId, CancellationToken ct = default) =>
        ReadAsync(c =>
        {
            var lines = c.Query<ArchiveRow>(
                "SELECT extractor, video_id FROM download_archive WHERE creator_id = @creatorId;",
                new { creatorId })
                .Select(r => $"{r.Extractor} {r.VideoId}");
            var path = Path.Combine(Path.GetTempPath(), $"pa-archive-{creatorId}-{Guid.NewGuid():N}.txt");
            File.WriteAllLines(path, lines);
            return path;
        }, ct);

    public Task AddHistoryAsync(DownloadResult result, long? postId, CancellationToken ct = default) =>
        WriteAsync(c =>
        {
            var finished = _clock.UtcNow;
            var started = finished - result.Elapsed;
            return c.Execute(
                """
                INSERT INTO download_history(post_id, source_url, outcome, exit_code, error, started_at, finished_at)
                VALUES (@postId, @sourceUrl, @outcome, @exitCode, @error, @startedAt, @finishedAt);
                """,
                new
                {
                    postId,
                    sourceUrl = result.Job.SourceUrl,
                    outcome = (int)result.Outcome,
                    exitCode = result.ExitCode,
                    error = result.Error,
                    startedAt = Conv.Iso(started),
                    finishedAt = Conv.Iso(finished),
                });
        }, ct);

    private Task<T> ReadAsync<T>(Func<SqliteConnection, T> work, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var connection = _factory.Create();
            return work(connection);
        }, ct);

    private async Task<T> WriteAsync<T>(Func<SqliteConnection, T> work, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var connection = _factory.Create();
                return work(connection);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private Task<int> WriteAsync(Func<SqliteConnection, int> work, CancellationToken ct) =>
        WriteAsync<int>(work, ct);

    public void Dispose() => _writeGate.Dispose();
}
