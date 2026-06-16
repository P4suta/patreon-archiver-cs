namespace PatreonArchiver.Core.Domain;

/// <summary>The outcome of a disk-space check: whether there is headroom, and the numbers behind it.</summary>
public readonly record struct DiskSpaceStatus(bool HasHeadroom, long AvailableBytes, long RequiredBytes);
