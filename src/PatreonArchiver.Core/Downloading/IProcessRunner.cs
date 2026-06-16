namespace PatreonArchiver.Core.Downloading;

/// <summary>
/// Runs an external process, streaming its stdout/stderr line by line. Abstracted so the download
/// engine can be unit-tested with a fake that emits canned output.
/// </summary>
internal interface IProcessRunner
{
    Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutputLine,
        Action<string> onErrorLine,
        CancellationToken ct);
}
