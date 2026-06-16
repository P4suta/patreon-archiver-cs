using System.Text;
using PatreonArchiver.Core.Parsing;

namespace PatreonArchiver.Core.Tests.Parsing;

public sealed class MimeMhtmlExtractorTests
{
    [Fact]
    public async Task Extracts_and_decodes_the_html_part()
    {
        var mhtml = Crlf(
            "From: <Saved by Blink>",
            "Subject: Test Page",
            "MIME-Version: 1.0",
            "Content-Type: multipart/related; type=\"text/html\"; boundary=\"----MultipartBoundary\"",
            "",
            "------MultipartBoundary",
            "Content-Type: text/html",
            "Content-Transfer-Encoding: quoted-printable",
            "Content-Location: https://example.com/",
            "",
            "<html><body><div data-tag=3D\"post-card\">hello</div></body></html>",
            "------MultipartBoundary",
            "Content-Type: image/png",
            "Content-Transfer-Encoding: base64",
            "Content-Location: https://example.com/x.png",
            "",
            "iVBORw0KGgo=",
            "------MultipartBoundary--",
            "");

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(mhtml));
        var html = await new MimeMhtmlExtractor().ExtractHtmlAsync(stream);

        Assert.Contains("data-tag=\"post-card\"", html);   // =3D decoded to =
        Assert.DoesNotContain("=3D", html);
        Assert.DoesNotContain("iVBORw0KGgo", html);         // the image part is not returned
    }

    [Fact]
    public async Task Throws_when_no_html_part_is_present()
    {
        var mhtml = Crlf(
            "MIME-Version: 1.0",
            "Content-Type: multipart/related; boundary=\"----B\"",
            "",
            "------B",
            "Content-Type: text/plain",
            "",
            "no html here",
            "------B--",
            "");

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(mhtml));
        await Assert.ThrowsAsync<InvalidDataException>(() => new MimeMhtmlExtractor().ExtractHtmlAsync(stream));
    }

    private static string Crlf(params string[] lines) => string.Join("\r\n", lines);
}
