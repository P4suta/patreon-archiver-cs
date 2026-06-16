using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Downloading;

namespace PatreonArchiver.Core.Tests.Downloading;

public sealed class ToolLocatorTests
{
    [Fact]
    public void Uses_the_explicit_tools_directory()
    {
        var locator = new ToolLocator(Options.Create(MakeOptions(@"C:\tools")));

        var paths = locator.Resolve();

        Assert.Equal(@"C:\tools\yt-dlp.exe", paths.YtDlp);
        Assert.Equal(@"C:\tools\ffmpeg.exe", paths.Ffmpeg);
        Assert.Equal(@"C:\tools", paths.FfmpegDirectory);
    }

    [Fact]
    public void Defaults_to_the_app_tools_folder()
    {
        var locator = new ToolLocator(Options.Create(MakeOptions(toolsDirectory: null)));

        var paths = locator.Resolve();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe"), paths.YtDlp);
    }

    private static CoreOptions MakeOptions(string? toolsDirectory) => new()
    {
        DatabasePath = "db",
        StagingRoot = "stage",
        OutputRoot = "out",
        ToolsDirectory = toolsDirectory,
    };
}
