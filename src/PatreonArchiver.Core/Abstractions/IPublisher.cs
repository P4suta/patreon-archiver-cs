namespace PatreonArchiver.Core.Abstractions;

/// <summary>
/// Atomically publishes a staged file into the library: the destination directory never
/// contains a partial file (staging → temp-in-destination → same-volume rename).
/// </summary>
public interface IPublisher
{
    /// <summary>Publishes <paramref name="stagedFile"/> as <paramref name="finalName"/> and returns the final path.</summary>
    Task<string> PublishAsync(string stagedFile, string destinationDir, string finalName, CancellationToken ct = default);
}
