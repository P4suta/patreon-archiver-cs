using PatreonArchiver.Core.Configuration;

namespace PatreonArchiver.Core.Abstractions;

/// <summary>Resolves the on-disk locations of the bundled yt-dlp / ffmpeg executables.</summary>
public interface IToolLocator
{
    ToolPaths Resolve();
}
