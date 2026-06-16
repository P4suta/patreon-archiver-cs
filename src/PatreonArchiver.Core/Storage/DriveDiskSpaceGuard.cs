using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Storage;

/// <summary>
/// Disk-space guard backed by <see cref="DriveInfo"/>. Reports the minimum free space across the
/// staging and output volumes. A probe failure yields <see cref="long.MaxValue"/> so a transient
/// error never falsely blocks downloads.
/// </summary>
internal sealed class DriveDiskSpaceGuard(IOptions<CoreOptions> options) : IDiskSpaceGuard
{
    public DiskSpaceStatus Check(long minimumFreeBytes)
    {
        var available = Math.Min(
            AvailableFreeBytes(options.Value.StagingRoot),
            AvailableFreeBytes(options.Value.OutputRoot));
        return new DiskSpaceStatus(available >= minimumFreeBytes, available, minimumFreeBytes);
    }

    private static long AvailableFreeBytes(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrEmpty(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return long.MaxValue;
        }
    }
}
