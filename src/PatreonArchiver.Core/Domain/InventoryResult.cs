namespace PatreonArchiver.Core.Domain;

/// <summary>The outcome of parsing a creator's posts page (live WebView2 DOM or imported MHTML).</summary>
public sealed record InventoryResult(IReadOnlyList<Post> Posts)
{
    public int VideoPostCount => Posts.Count;

    /// <summary>Oldest post date in the snapshot (the original tool's <c>mhtml_date_range</c> minimum).</summary>
    public DateOnly? OldestDate => Posts.Count == 0 ? null : Posts.Min(p => p.Date);

    /// <summary>Newest post date in the snapshot.</summary>
    public DateOnly? NewestDate => Posts.Count == 0 ? null : Posts.Max(p => p.Date);

    public static InventoryResult Empty { get; } = new([]);
}
