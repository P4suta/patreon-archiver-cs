using PatreonArchiver.Core.Abstractions;

namespace PatreonArchiver.Core;

/// <summary>The real system clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
