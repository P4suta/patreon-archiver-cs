using PatreonArchiver.Core.Abstractions;

namespace PatreonArchiver.Core.Tests;

/// <summary>A controllable clock for deterministic tests.</summary>
internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
