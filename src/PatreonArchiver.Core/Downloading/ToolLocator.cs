using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;

namespace PatreonArchiver.Core.Downloading;

/// <summary>
/// Resolves the bundled tool executables. For the unpackaged, self-contained build they live next
/// to the app in a <c>tools/</c> folder (<see cref="AppContext.BaseDirectory"/>); a host may override
/// the directory via <see cref="CoreOptions.ToolsDirectory"/>.
/// </summary>
internal sealed class ToolLocator(IOptions<CoreOptions> options) : IToolLocator
{
    public ToolPaths Resolve()
    {
        var directory = string.IsNullOrWhiteSpace(options.Value.ToolsDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "tools")
            : options.Value.ToolsDirectory;

        return new ToolPaths(
            Path.Combine(directory, "yt-dlp.exe"),
            Path.Combine(directory, "ffmpeg.exe"),
            Path.Combine(directory, "ffprobe.exe"));
    }
}
