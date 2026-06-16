namespace PatreonArchiver.Core.Domain;

/// <summary>An archivable video post belonging to a <see cref="Creator"/>.</summary>
public sealed record Post
{
    public required long Id { get; init; }
    public required long CreatorId { get; init; }
    public required StreamReference Stream { get; init; }

    public string? Title { get; init; }
    public Uri? ResolvedIframe { get; init; }
    public string? PatreonPostUrl { get; init; }

    /// <summary>yt-dlp <c>%(id)s</c> — the stable dedup key, populated after a download.</summary>
    public string? VideoId { get; init; }

    public PostStatus Status { get; init; } = PostStatus.Discovered;
    public string? FilePath { get; init; }

    public DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The per-creator unique key ("{yyyymmdd}_{slug}_{token}").</summary>
    public string Token => Stream.Segment;

    /// <summary>The publish date.</summary>
    public DateOnly Date => Stream.Date;
}
