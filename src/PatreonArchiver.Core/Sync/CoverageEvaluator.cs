namespace PatreonArchiver.Core.Sync;

/// <summary>The proposed anchor advance plus an optional gap warning.</summary>
internal readonly record struct CoverageDecision(DateOnly? ProposedAnchor, string? GapWarning)
{
    public bool HasGap => GapWarning is not null;
}

/// <summary>
/// Pure coverage-anchor gap detection, ported verbatim from the original <c>sync.py</c>:
/// if the snapshot's oldest date reaches back to (≤) the prior anchor, coverage is continuous and
/// the anchor advances to the newest date; otherwise a gap is flagged and the anchor is held.
/// </summary>
internal static class CoverageEvaluator
{
    public static CoverageDecision Evaluate(DateOnly? previousAnchor, DateOnly? oldest, DateOnly? newest)
    {
        if (oldest is null || newest is null)
        {
            return new CoverageDecision(previousAnchor, null);   // nothing to anchor
        }

        if (previousAnchor is null)
        {
            return new CoverageDecision(newest, null);           // first run: initialize
        }

        if (oldest <= previousAnchor)
        {
            return new CoverageDecision(Later(previousAnchor.Value, newest.Value), null);  // continuous
        }

        var warning =
            $"Coverage gap pending: the snapshot's oldest post ({oldest:yyyy-MM-dd}) is newer than the last " +
            $"anchored date ({previousAnchor:yyyy-MM-dd}). Visible posts are still downloaded, but the gap " +
            $"stays open until a deeper snapshot reaches back to {previousAnchor:yyyy-MM-dd} or earlier.";
        return new CoverageDecision(previousAnchor, warning);    // hold the anchor
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a >= b ? a : b;
}
