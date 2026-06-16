using System.Text;
using MimeKit;
using PatreonArchiver.Core.Abstractions;

namespace PatreonArchiver.Core.Parsing;

/// <summary>
/// Extracts the primary <c>text/html</c> part from an MHTML (<c>multipart/related</c>) snapshot,
/// decoding quoted-printable/base64 transfer encodings — the C# analogue of Python's
/// <c>email</c> + <c>get_payload(decode=True)</c>.
/// </summary>
internal sealed class MimeMhtmlExtractor : IMhtmlExtractor
{
    public async Task<string> ExtractHtmlAsync(Stream mhtml, CancellationToken ct = default)
    {
        var message = await MimeMessage.LoadAsync(mhtml, ct).ConfigureAwait(false);

        var htmlPart = message.BodyParts
            .OfType<MimePart>()
            .FirstOrDefault(part => part.ContentType.IsMimeType("text", "html"))
            ?? throw new InvalidDataException("No text/html part found in the MHTML snapshot.");

        var content = htmlPart.Content
            ?? throw new InvalidDataException("The text/html MHTML part has no content.");

        using var buffer = new MemoryStream();
        await content.DecodeToAsync(buffer, ct).ConfigureAwait(false);
        buffer.Position = 0;

        var encoding = htmlPart.ContentType.CharsetEncoding ?? Encoding.UTF8;
        using var reader = new StreamReader(buffer, encoding);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }
}
