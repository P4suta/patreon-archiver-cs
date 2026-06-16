using PatreonArchiver.Core.Cookies;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Tests.Cookies;

public sealed class NetscapeCookieExporterTests
{
    [Fact]
    public void Formats_header_and_tab_separated_lines()
    {
        var content = NetscapeCookieExporter.Format(
        [
            new Cookie(".patreon.com", IncludeSubdomains: true, "/", Secure: true, 1893456000, "session_id", "abc"),
            new Cookie("stream.acme.tv", IncludeSubdomains: false, "/", Secure: false, 0, "t", "v"),
        ]);

        Assert.StartsWith("# Netscape HTTP Cookie File\n", content);
        Assert.Contains(".patreon.com\tTRUE\t/\tTRUE\t1893456000\tsession_id\tabc\n", content);
        Assert.Contains("stream.acme.tv\tFALSE\t/\tFALSE\t0\tt\tv\n", content);
    }

    [Fact]
    public async Task ExportAsync_writes_the_formatted_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pa-cookies-{Guid.NewGuid():N}.txt");
        var cookies = new[] { new Cookie(".patreon.com", true, "/", true, 0, "s", "v") };

        try
        {
            await new NetscapeCookieExporter().ExportAsync(path, cookies);
            Assert.Equal(NetscapeCookieExporter.Format(cookies), await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
