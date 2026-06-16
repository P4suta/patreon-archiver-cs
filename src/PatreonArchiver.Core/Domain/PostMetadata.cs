namespace PatreonArchiver.Core.Domain;

/// <summary>
/// Curated metadata injected into a download, parsed from inventory or a batch
/// "# key: value" header block. Maps onto yt-dlp <c>--parse-metadata</c> flags.
/// </summary>
public sealed record PostMetadata
{
    public string? Uploader { get; init; }
    public string? Title { get; init; }
    public DateOnly? Date { get; init; }
    public string? PostUrl { get; init; }

    public static PostMetadata Empty { get; } = new();
}
