namespace PatreonArchiver.Core.Domain;

/// <summary>Lifecycle of an archivable post, from discovery to a published file on disk.</summary>
public enum PostStatus
{
    /// <summary>Seen on a page, not yet processed.</summary>
    Discovered,

    /// <summary>Stream URL resolved to a downloadable target.</summary>
    Resolved,

    /// <summary>Currently downloading.</summary>
    Downloading,

    /// <summary>Successfully downloaded and atomically published.</summary>
    Published,

    /// <summary>Already in the archive; nothing to do.</summary>
    Skipped,

    /// <summary>A download attempt failed; eligible for retry on the next sync.</summary>
    Failed,
}
