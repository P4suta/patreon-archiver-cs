using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>
/// Parses a creator's posts page into an <see cref="InventoryResult"/>. The HTML may come from
/// a live WebView2 DOM snapshot or from extracted MHTML — handling is identical.
/// </summary>
public interface IPostPageParser
{
    InventoryResult Parse(string html);
}
