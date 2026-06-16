namespace PatreonArchiver.Core.Sync;

/// <summary>Synchronously forwards progress reports to a delegate (no SynchronizationContext capture).</summary>
internal sealed class DelegateProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
