using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>Writes captured cookies to a yt-dlp-compatible Netscape <c>cookies.txt</c> file.</summary>
public interface ICookieExporter
{
    Task ExportAsync(string filePath, IReadOnlyCollection<Cookie> cookies, CancellationToken ct = default);
}
