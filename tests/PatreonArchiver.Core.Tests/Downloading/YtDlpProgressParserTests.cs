using PatreonArchiver.Core.Downloading;

namespace PatreonArchiver.Core.Tests.Downloading;

public sealed class YtDlpProgressParserTests
{
    [Fact]
    public void Parses_a_full_progress_line()
    {
        Assert.True(YtDlpProgressParser.TryParse("[download]  42.7% of 350.21MiB at 5.10MiB/s ETA 00:30", out var p));
        Assert.Equal(42.7, p.Percent);
        Assert.Equal("350.21MiB", p.TotalSize);
        Assert.Equal("5.10MiB/s", p.Speed);
        Assert.Equal("00:30", p.Eta);
    }

    [Fact]
    public void Parses_a_bare_percentage()
    {
        Assert.True(YtDlpProgressParser.TryParse("[download] 100% of 10.00MiB", out var p));
        Assert.Equal(100, p.Percent);
        Assert.Equal("10.00MiB", p.TotalSize);
        Assert.Null(p.Speed);
        Assert.Null(p.Eta);
    }

    [Fact]
    public void Handles_unknown_size_marker()
    {
        Assert.True(YtDlpProgressParser.TryParse("[download]   0.0% of ~  5.00MiB at  1.00KiB/s ETA 00:10", out var p));
        Assert.Equal(0.0, p.Percent);
        Assert.Equal("5.00MiB", p.TotalSize);
    }

    [Theory]
    [InlineData("[info] Writing video subtitles")]
    [InlineData("[download] Destination: video.mp4")]
    [InlineData("just noise")]
    public void Rejects_non_progress_lines(string line)
    {
        Assert.False(YtDlpProgressParser.TryParse(line, out _));
    }
}
