using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;
using PatreonArchiver.Core.Downloading;
using PatreonArchiver.Core.Publishing;

namespace PatreonArchiver.Core.Tests.Downloading;

public sealed class YtDlpDownloadEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pa-eng-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public async Task Successful_download_reports_progress_and_publishes_the_file()
    {
        var runner = new ScriptedRunner((args, onOut, _) =>
        {
            var staging = StagingDir(args);
            onOut("[download]  50.0% of 10.00MiB at 1.00MiB/s ETA 00:05");
            onOut("[download] 100% of 10.00MiB");
            var file = Path.Combine(staging, "acme", "2026-01-15_ep.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "video-bytes");
            onOut($"{YtDlpArgBuilder.DoneMarker} generic\tvid123\t{file}");
            return 0;
        });
        var engine = NewEngine(runner);
        var progress = new ListProgress();

        var result = await engine.DownloadAsync(new DownloadJob { SourceUrl = "https://stream.acme.tv/20260115_ep_tok/" }, progress);

        Assert.Equal(DownloadOutcome.Success, result.Outcome);
        Assert.Equal("vid123", result.VideoId);
        Assert.Equal("generic", result.Extractor);
        Assert.NotNull(result.PublishedPath);
        Assert.True(File.Exists(result.PublishedPath));
        Assert.Equal("video-bytes", await File.ReadAllTextAsync(result.PublishedPath!));
        Assert.Contains(progress.Items, p => p.Percent == 100);
    }

    [Fact]
    public async Task Non_zero_exit_is_a_failure_carrying_the_stderr_tail()
    {
        var runner = new ScriptedRunner((_, _, onErr) => { onErr("ERROR: boom"); return 5; });

        var result = await NewEngine(runner).DownloadAsync(new DownloadJob { SourceUrl = "u" });

        Assert.Equal(DownloadOutcome.Failed, result.Outcome);
        Assert.Equal(5, result.ExitCode);
        Assert.Contains("boom", result.Error);
    }

    [Fact]
    public async Task Archive_skip_is_reported_as_already_archived()
    {
        var runner = new ScriptedRunner((_, onOut, _) =>
        {
            onOut("[generic] video has already been recorded in the archive");
            return 0;
        });

        var result = await NewEngine(runner).DownloadAsync(new DownloadJob { SourceUrl = "u" });

        Assert.Equal(DownloadOutcome.AlreadyArchived, result.Outcome);
    }

    [Fact]
    public async Task Simulation_succeeds_without_publishing()
    {
        var runner = new ScriptedRunner((_, _, _) => 0);

        var result = await NewEngine(runner).DownloadAsync(new DownloadJob { SourceUrl = "u", Simulate = true });

        Assert.Equal(DownloadOutcome.Success, result.Outcome);
        Assert.Null(result.PublishedPath);
    }

    [Fact]
    public async Task Cancellation_is_reported_as_cancelled()
    {
        var runner = new ScriptedRunner((_, _, _) => throw new OperationCanceledException());

        var result = await NewEngine(runner).DownloadAsync(new DownloadJob { SourceUrl = "u" });

        Assert.Equal(DownloadOutcome.Cancelled, result.Outcome);
    }

    private YtDlpDownloadEngine NewEngine(IProcessRunner runner)
    {
        var options = Options.Create(new CoreOptions
        {
            DatabasePath = Path.Combine(_root, "state.db"),
            StagingRoot = Path.Combine(_root, "staging"),
            OutputRoot = Path.Combine(_root, "library"),
        });
        var tools = new StubToolLocator(new ToolPaths(@"C:\tools\yt-dlp.exe", @"C:\tools\ffmpeg.exe", null));
        return new YtDlpDownloadEngine(runner, new AtomicPublisher(), tools, options);
    }

    private static string StagingDir(IReadOnlyList<string> args)
    {
        var index = args.ToList().IndexOf("--paths");
        return args[index + 1]["home:".Length..];
    }

    private sealed class ScriptedRunner(Func<IReadOnlyList<string>, Action<string>, Action<string>, int> script) : IProcessRunner
    {
        public Task<int> RunAsync(
            string fileName, IReadOnlyList<string> arguments,
            Action<string> onOutputLine, Action<string> onErrorLine, CancellationToken ct) =>
            Task.FromResult(script(arguments, onOutputLine, onErrorLine));
    }

    private sealed class StubToolLocator(ToolPaths paths) : IToolLocator
    {
        public ToolPaths Resolve() => paths;
    }

    private sealed class ListProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Items { get; } = [];

        public void Report(DownloadProgress value) => Items.Add(value);
    }
}
