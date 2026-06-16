using System.Security.Cryptography;
using PatreonArchiver.Core.Abstractions;

namespace PatreonArchiver.Core.Publishing;

/// <summary>
/// Publishes a staged file by copying it to a hidden temp file in the destination directory, then
/// renaming it into place. The rename is same-volume (hence atomic), so the library never contains
/// a partial file. Ports <c>publish.py</c>'s contract.
/// </summary>
internal sealed class AtomicPublisher : IPublisher
{
    public async Task<string> PublishAsync(
        string stagedFile, string destinationDir, string finalName, CancellationToken ct = default)
    {
        // finalName may carry a relative subpath (e.g. "uploader/2026-01-01_title.mp4").
        var destination = Path.GetFullPath(Path.Combine(destinationDir, finalName));
        var finalDir = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("Destination has no directory.", nameof(finalName));
        Directory.CreateDirectory(finalDir);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var temp = Path.Combine(finalDir, $".pa-publish.{token}.tmp");

        try
        {
            await using (var source = File.OpenRead(stagedFile))
            await using (var sink = File.Create(temp))
            {
                await source.CopyToAsync(sink, ct).ConfigureAwait(false);
            }

            // Same-volume rename == atomic publish (overwrite makes re-publishing idempotent).
            File.Move(temp, destination, overwrite: true);
            return destination;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
