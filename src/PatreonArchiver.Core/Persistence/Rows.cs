using System.Globalization;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Persistence;

/// <summary>Shared TEXT ↔ typed conversions for the SQLite storage format.</summary>
internal static class Conv
{
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Iso(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);

    public static DateOnly? Date(string? text) =>
        text is null ? null : DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTimeOffset Time(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}

internal sealed class CreatorRow
{
    public long Id { get; set; }
    public string Handle { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? StreamHost { get; set; }
    public string? PatreonUrl { get; set; }

    public Creator ToDomain() => new(Id, Handle, DisplayName, StreamHost, PatreonUrl);
}

internal sealed class PostRow
{
    public long Id { get; set; }
    public long CreatorId { get; set; }
    public string Token { get; set; } = "";
    public string StreamUrl { get; set; } = "";
    public string PostDate { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Title { get; set; }
    public string? ResolvedIframe { get; set; }
    public string? PatreonPostUrl { get; set; }
    public string? VideoId { get; set; }
    public int Status { get; set; }
    public string? FilePath { get; set; }
    public string DiscoveredAt { get; set; } = "";
    public string? PublishedAt { get; set; }

    public Post ToDomain()
    {
        if (!StreamReference.TryParse(StreamUrl, out var stream))
        {
            throw new InvalidOperationException($"Stored stream URL is no longer parseable: {StreamUrl}");
        }

        return new Post
        {
            Id = Id,
            CreatorId = CreatorId,
            Stream = stream,
            Title = Title,
            ResolvedIframe = ResolvedIframe is null ? null : new Uri(ResolvedIframe),
            PatreonPostUrl = PatreonPostUrl,
            VideoId = VideoId,
            Status = (PostStatus)Status,
            FilePath = FilePath,
            DiscoveredAt = Conv.Time(DiscoveredAt),
            PublishedAt = PublishedAt is null ? null : Conv.Time(PublishedAt),
        };
    }
}

internal sealed class AnchorRow
{
    public long CreatorId { get; set; }
    public string? AnchorDate { get; set; }
    public string? PendingGapFrom { get; set; }
    public string? PendingGapTo { get; set; }
    public string UpdatedAt { get; set; } = "";

    public CoverageAnchor ToDomain() =>
        new(CreatorId, Conv.Date(AnchorDate), Conv.Date(PendingGapFrom), Conv.Date(PendingGapTo), Conv.Time(UpdatedAt));
}

internal sealed class ArchiveRow
{
    public string Extractor { get; set; } = "";
    public string VideoId { get; set; } = "";
}
