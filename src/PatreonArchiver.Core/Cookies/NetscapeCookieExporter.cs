using System.Text;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Cookies;

/// <summary>Writes cookies in the Netscape <c>cookies.txt</c> format that yt-dlp consumes.</summary>
internal sealed class NetscapeCookieExporter : ICookieExporter
{
    public Task ExportAsync(string filePath, IReadOnlyCollection<Cookie> cookies, CancellationToken ct = default) =>
        File.WriteAllTextAsync(filePath, Format(cookies), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

    /// <summary>Renders the file content. Pure, so it can be asserted directly.</summary>
    public static string Format(IEnumerable<Cookie> cookies)
    {
        var builder = new StringBuilder();
        builder.Append("# Netscape HTTP Cookie File\n");
        foreach (var cookie in cookies)
        {
            builder
                .Append(cookie.Domain).Append('\t')
                .Append(Flag(cookie.IncludeSubdomains)).Append('\t')
                .Append(cookie.Path).Append('\t')
                .Append(Flag(cookie.Secure)).Append('\t')
                .Append(cookie.Expires).Append('\t')
                .Append(cookie.Name).Append('\t')
                .Append(cookie.Value).Append('\n');
        }

        return builder.ToString();

        static string Flag(bool value) => value ? "TRUE" : "FALSE";
    }
}
