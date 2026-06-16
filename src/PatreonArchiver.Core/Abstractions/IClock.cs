namespace PatreonArchiver.Core.Abstractions;

/// <summary>Abstracts the current time so coverage/timestamp logic stays testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
