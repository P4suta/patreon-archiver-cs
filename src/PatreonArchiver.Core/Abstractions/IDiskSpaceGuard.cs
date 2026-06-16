using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>Guards downloads against filling the disk: checks the staging and output volumes' free space.</summary>
public interface IDiskSpaceGuard
{
    /// <summary>Reports whether the relevant volumes have at least <paramref name="minimumFreeBytes"/> free.</summary>
    DiskSpaceStatus Check(long minimumFreeBytes);
}
