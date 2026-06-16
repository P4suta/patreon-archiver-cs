namespace PatreonArchiver.Core.Abstractions;

/// <summary>Extracts the primary <c>text/html</c> part from an MHTML (MIME multipart) snapshot.</summary>
public interface IMhtmlExtractor
{
    Task<string> ExtractHtmlAsync(Stream mhtml, CancellationToken ct = default);
}
