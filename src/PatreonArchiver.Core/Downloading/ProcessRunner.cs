using System.Diagnostics;
using System.Text;

namespace PatreonArchiver.Core.Downloading;

/// <summary>Real <see cref="IProcessRunner"/> over <see cref="System.Diagnostics.Process"/>.</summary>
internal sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutputLine,
        Action<string> onErrorLine,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { onOutputLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { onErrorLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // process already exited
            }
        }))
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        return process.ExitCode;
    }
}
