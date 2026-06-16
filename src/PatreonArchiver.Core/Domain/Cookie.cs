namespace PatreonArchiver.Core.Domain;

/// <summary>
/// A browser cookie captured from the WebView2 session, in the shape needed to write a
/// Netscape <c>cookies.txt</c> for yt-dlp fallback auth.
/// </summary>
public sealed record Cookie(
    string Domain,
    bool IncludeSubdomains,
    string Path,
    bool Secure,
    long Expires,
    string Name,
    string Value);
