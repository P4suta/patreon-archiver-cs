using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;
using PatreonArchiver.Core.Downloading;

namespace PatreonArchiver.Core.Tests.Downloading;

public sealed class YtDlpArgBuilderTests
{
    private static readonly ToolPaths Tools = new(@"C:\tools\yt-dlp.exe", @"C:\tools\ffmpeg.exe", null);

    [Fact]
    public void Emits_core_flags_paths_and_source_url()
    {
        var args = Build(new DownloadJob { SourceUrl = "https://stream.acme.tv/20260115_ep_tok/" });

        Assert.Equal(@"C:\tools", Adjacent(args, "--ffmpeg-location"));
        Assert.Equal(@"home:C:\stage", Adjacent(args, "--paths"));
        Assert.Equal("%(uploader)s/%(upload_date>%Y-%m-%d)s_%(title).140B.%(ext)s", Adjacent(args, "--output"));
        Assert.Contains("--embed-metadata", args);
        Assert.Contains("--newline", args);
        Assert.Contains("--ignore-config", args);
        Assert.Equal("https://stream.acme.tv/20260115_ep_tok/", args[^1]); // source url is last
    }

    [Theory]
    [InlineData(PresetKind.Polite, "4", "1")]
    [InlineData(PresetKind.Fast, "8", "0")]
    public void Applies_preset_concurrency_and_sleep(PresetKind kind, string fragments, string sleep)
    {
        var args = Build(new DownloadJob { SourceUrl = "u", Preset = kind });

        Assert.Equal(fragments, Adjacent(args, "--concurrent-fragments"));
        Assert.Equal(sleep, Adjacent(args, "--sleep-requests"));
    }

    [Fact]
    public void Includes_archive_and_cookies_when_provided()
    {
        var args = Build(new DownloadJob { SourceUrl = "u" }, archive: @"C:\arch.txt", cookies: @"C:\cookies.txt");

        Assert.Equal(@"C:\arch.txt", Adjacent(args, "--download-archive"));
        Assert.Equal(@"C:\cookies.txt", Adjacent(args, "--cookies"));
    }

    [Fact]
    public void Omits_archive_and_cookies_when_absent()
    {
        var args = Build(new DownloadJob { SourceUrl = "u" });

        Assert.DoesNotContain("--download-archive", args);
        Assert.DoesNotContain("--cookies", args);
    }

    [Fact]
    public void Injects_metadata_with_sentinel_and_escaped_colons()
    {
        var args = Build(new DownloadJob
        {
            SourceUrl = "u",
            Metadata = new PostMetadata
            {
                Uploader = "acme",
                Title = "Ep 1: Intro",
                Date = new DateOnly(2026, 1, 15),
                PostUrl = "https://patreon.com/posts/1",
            },
        });

        Assert.Contains(@"= 20260115:= %(upload_date)s", args);
        Assert.Contains(@"= Ep 1\: Intro:= %(title)s", args);     // colon escaped, sentinel preserved
        Assert.Contains(@"= Ep 1\: Intro:= %(meta_title)s", args);
        Assert.Contains(@"= acme:= %(uploader)s", args);
        Assert.Contains(@"= https\://patreon.com/posts/1:= %(meta_purl)s", args);
    }

    [Fact]
    public void Adds_simulate_flag_for_dry_runs()
    {
        Assert.Contains("--simulate", Build(new DownloadJob { SourceUrl = "u", Simulate = true }));
        Assert.DoesNotContain("--simulate", Build(new DownloadJob { SourceUrl = "u" }));
    }

    private static IReadOnlyList<string> Build(DownloadJob job, string? archive = null, string? cookies = null) =>
        YtDlpArgBuilder.Build(new YtDlpInvocation(job, Tools, @"C:\stage", archive, cookies));

    private static string Adjacent(IReadOnlyList<string> args, string flag)
    {
        var index = args.ToList().IndexOf(flag);
        Assert.True(index >= 0 && index + 1 < args.Count, $"flag '{flag}' not found with a value");
        return args[index + 1];
    }
}
