using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Storage;

namespace PatreonArchiver.Core.Tests.Storage;

public sealed class DriveDiskSpaceGuardTests
{
    [Fact]
    public void Reports_headroom_for_a_tiny_threshold()
    {
        var guard = new DriveDiskSpaceGuard(Options.Create(NewOptions()));

        var status = guard.Check(minimumFreeBytes: 1);

        Assert.True(status.HasHeadroom);
        Assert.True(status.AvailableBytes > 0);
        Assert.Equal(1, status.RequiredBytes);
    }

    [Fact]
    public void Reports_no_headroom_for_an_impossible_threshold()
    {
        var guard = new DriveDiskSpaceGuard(Options.Create(NewOptions()));

        var status = guard.Check(minimumFreeBytes: long.MaxValue);

        Assert.False(status.HasHeadroom);
    }

    private static CoreOptions NewOptions() => new()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), "pa.db"),
        StagingRoot = Path.GetTempPath(),
        OutputRoot = Path.GetTempPath(),
    };
}
