using PatreonArchiver.Core.Sync;

namespace PatreonArchiver.Core.Tests.Sync;

public sealed class CoverageEvaluatorTests
{
    [Fact]
    public void First_run_anchors_to_newest()
    {
        var decision = CoverageEvaluator.Evaluate(previousAnchor: null, D(1, 1), D(1, 10));

        Assert.Equal(D(1, 10), decision.ProposedAnchor);
        Assert.False(decision.HasGap);
    }

    [Fact]
    public void Continuous_coverage_advances_to_newest()
    {
        var decision = CoverageEvaluator.Evaluate(D(1, 10), oldest: D(1, 5), newest: D(1, 20));

        Assert.Equal(D(1, 20), decision.ProposedAnchor);
        Assert.False(decision.HasGap);
    }

    [Fact]
    public void Continuous_coverage_keeps_anchor_when_newest_is_older()
    {
        var decision = CoverageEvaluator.Evaluate(D(1, 20), oldest: D(1, 5), newest: D(1, 15));

        Assert.Equal(D(1, 20), decision.ProposedAnchor); // never moves backwards
        Assert.False(decision.HasGap);
    }

    [Fact]
    public void Gap_is_flagged_and_anchor_held_when_oldest_is_after_anchor()
    {
        var decision = CoverageEvaluator.Evaluate(D(1, 10), oldest: D(2, 1), newest: D(2, 10));

        Assert.Equal(D(1, 10), decision.ProposedAnchor); // held
        Assert.True(decision.HasGap);
        Assert.Contains("gap pending", decision.GapWarning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_dates_keeps_the_previous_anchor()
    {
        var decision = CoverageEvaluator.Evaluate(D(1, 10), oldest: null, newest: null);

        Assert.Equal(D(1, 10), decision.ProposedAnchor);
        Assert.False(decision.HasGap);
    }

    private static DateOnly D(int month, int day) => new(2026, month, day);
}
