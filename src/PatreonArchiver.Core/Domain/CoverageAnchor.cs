namespace PatreonArchiver.Core.Domain;

/// <summary>
/// Per-creator coverage anchor for gap detection (replaces the original <c>coverage.txt</c>).
/// The anchor is the newest date up to which coverage is known to be continuous; a pending
/// gap window is held open until a deeper snapshot reaches back to the prior anchor.
/// </summary>
public sealed record CoverageAnchor(
    long CreatorId,
    DateOnly? AnchorDate,
    DateOnly? PendingGapFrom,
    DateOnly? PendingGapTo,
    DateTimeOffset UpdatedAt)
{
    public bool HasGap => PendingGapFrom is not null;

    public static CoverageAnchor None(long creatorId, DateTimeOffset now) =>
        new(creatorId, null, null, null, now);
}
