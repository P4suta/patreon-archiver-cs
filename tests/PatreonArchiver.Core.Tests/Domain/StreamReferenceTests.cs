using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Tests.Domain;

public sealed class StreamReferenceTests
{
    [Fact]
    public void Parses_a_well_formed_stream_url()
    {
        var ok = StreamReference.TryParse("https://stream.example.com/20260115_my-video_abc123/", out var reference);

        Assert.True(ok);
        Assert.Equal("stream.example.com", reference.Host);
        Assert.Equal(new DateOnly(2026, 1, 15), reference.Date);
        Assert.Equal("my-video", reference.Slug);
        Assert.Equal("abc123", reference.Token);
        Assert.Equal("20260115_my-video_abc123", reference.Segment);
    }

    [Fact]
    public void Parses_without_trailing_slash()
    {
        Assert.True(StreamReference.TryParse("https://stream.example.com/20260115_clip_tok", out var reference));
        Assert.Equal("20260115_clip_tok", reference.Segment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com/20260115_clip_tok/")]              // host is not stream.*
    [InlineData("https://stream.example.com/clip_tok/")]               // no 8-digit date
    [InlineData("https://stream.example.com/20261515_clip_tok/")]      // impossible month/day
    [InlineData("https://stream.example.com/2026011_clip_tok/")]       // 7 digits
    public void Rejects_malformed_urls(string? candidate)
    {
        Assert.False(StreamReference.TryParse(candidate, out _));
    }

    [Fact]
    public void Segment_round_trips_through_the_url()
    {
        const string url = "https://stream.creator.tv/20251231_year-end-special_zZ9-_.value/";
        Assert.True(StreamReference.TryParse(url, out var reference));
        Assert.Equal(new DateOnly(2025, 12, 31), reference.Date);
        Assert.Equal("year-end-special", reference.Slug);
    }
}
