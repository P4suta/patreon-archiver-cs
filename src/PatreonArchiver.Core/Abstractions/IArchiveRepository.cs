using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>
/// The persistence port for all archive state: creators, posts, the per-creator coverage
/// anchor, the dedup archive, and download history. Backed by SQLite. Replaces the original
/// tool's <c>seen_posts.txt</c> / <c>coverage.txt</c> / <c>archive.txt</c>.
/// </summary>
public interface IArchiveRepository
{
    /// <summary>Applies pending migrations; safe to call repeatedly.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    Task<Creator> GetOrCreateCreatorAsync(
        string handle, string? streamHost, string? patreonUrl, CancellationToken ct = default);

    /// <summary>Looks up a creator by handle without creating one (used by dry runs).</summary>
    Task<Creator?> FindCreatorAsync(string handle, CancellationToken ct = default);

    /// <summary>All known creators, ordered by handle.</summary>
    Task<IReadOnlyList<Creator>> GetCreatorsAsync(CancellationToken ct = default);

    /// <summary>The set of post tokens already known for this creator (for "what's new" diffing).</summary>
    Task<IReadOnlySet<string>> GetKnownTokensAsync(long creatorId, CancellationToken ct = default);

    /// <summary>Inserts unseen posts (as <see cref="PostStatus.Discovered"/>) and refreshes existing ones; returns them with assigned ids.</summary>
    Task<IReadOnlyList<Post>> UpsertPostsAsync(
        long creatorId, IEnumerable<Post> posts, CancellationToken ct = default);

    /// <summary>Posts eligible for download — status <see cref="PostStatus.Discovered"/> or <see cref="PostStatus.Failed"/> — oldest first.</summary>
    Task<IReadOnlyList<Post>> GetDownloadablePostsAsync(long creatorId, CancellationToken ct = default);

    Task<IReadOnlyList<Post>> GetPostsAsync(long creatorId, CancellationToken ct = default);

    Task MarkPostAsync(
        long postId, PostStatus status, string? filePath = null, string? videoId = null, CancellationToken ct = default);

    Task<CoverageAnchor> GetAnchorAsync(long creatorId, CancellationToken ct = default);
    Task AdvanceAnchorAsync(long creatorId, DateOnly anchor, CancellationToken ct = default);
    Task SetGapAsync(long creatorId, DateOnly gapFrom, DateOnly gapTo, CancellationToken ct = default);

    Task<bool> IsArchivedAsync(long creatorId, string extractor, string videoId, CancellationToken ct = default);
    Task RecordArchiveAsync(long creatorId, string extractor, string videoId, CancellationToken ct = default);

    /// <summary>Exports this creator's dedup archive to a temp file in yt-dlp <c>--download-archive</c> format.</summary>
    Task<string> ExportArchiveFileAsync(long creatorId, CancellationToken ct = default);

    Task AddHistoryAsync(DownloadResult result, long? postId, CancellationToken ct = default);
}
