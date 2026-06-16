namespace PatreonArchiver.Core.Abstractions;

/// <summary>
/// Resolves a source URL (Cloudflare Stream or a publisher post) to the URL yt-dlp should
/// consume — passing stream URLs through, or extracting the <c>iframe.videodelivery.net</c>
/// embed from a post page (the original <c>resolve.py</c>).
/// </summary>
public interface IUrlResolver
{
    Task<Uri> ResolveAsync(Uri source, CancellationToken ct = default);
}
