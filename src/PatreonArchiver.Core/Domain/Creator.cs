namespace PatreonArchiver.Core.Domain;

/// <summary>A creator whose stream-hosted videos are archived. The handle is the folder name and yt-dlp <c>%(uploader)s</c>.</summary>
public sealed record Creator(
    long Id,
    string Handle,
    string? DisplayName,
    string? StreamHost,
    string? PatreonUrl);
