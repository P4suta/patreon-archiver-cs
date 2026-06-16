using System.Diagnostics;
using Microsoft.Extensions.Options;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Downloading;

/// <summary>
/// Drives a single download through the bundled yt-dlp: builds the args, spawns the process,
/// streams progress, maps the exit to an outcome, and atomically publishes the produced file.
/// </summary>
internal sealed class YtDlpDownloadEngine : IDownloadEngine
{
    private const string AlreadyArchivedMarker = "has already been recorded in the archive";
    private const int ErrorTailLines = 40;

    private readonly IProcessRunner _runner;
    private readonly IPublisher _publisher;
    private readonly IToolLocator _toolLocator;
    private readonly CoreOptions _options;

    public YtDlpDownloadEngine(
        IProcessRunner runner,
        IPublisher publisher,
        IToolLocator toolLocator,
        IOptions<CoreOptions> options)
    {
        _runner = runner;
        _publisher = publisher;
        _toolLocator = toolLocator;
        _options = options.Value;
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadJob job, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var tools = _toolLocator.Resolve();
        var token = Guid.NewGuid().ToString("N");
        var stagingDir = Path.Combine(_options.StagingRoot, job.Retest ? "retest" : "stage", token);
        var outputRoot = job.Retest ? Path.Combine(_options.OutputRoot, ".retest", token) : _options.OutputRoot;
        Directory.CreateDirectory(stagingDir);

        var args = YtDlpArgBuilder.Build(new YtDlpInvocation(
            job, tools, stagingDir,
            ArchiveFile: job.Retest ? null : job.ArchiveFile,
            CookiesFile: job.CookiesFilePath));

        var capture = new Capture();
        var errorTail = new List<string>();
        var startedAt = Stopwatch.GetTimestamp();

        int exitCode;
        try
        {
            exitCode = await _runner.RunAsync(tools.YtDlp, args,
                line => HandleOutput(line, progress, capture),
                line => { if (errorTail.Count < 4096) { errorTail.Add(line); } },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryCleanup(stagingDir);
            return Result(job, DownloadOutcome.Cancelled, null, null, null, -1, "Cancelled.", startedAt);
        }

        try
        {
            if (exitCode != 0)
            {
                return Result(job, DownloadOutcome.Failed, null, capture.VideoId, capture.Extractor, exitCode, Tail(errorTail), startedAt);
            }

            if (capture.AlreadyArchived)
            {
                return Result(job, DownloadOutcome.AlreadyArchived, null, capture.VideoId, capture.Extractor, exitCode, null, startedAt);
            }

            if (job.Simulate)
            {
                return Result(job, DownloadOutcome.Success, null, capture.VideoId, capture.Extractor, exitCode, null, startedAt);
            }

            var stagedFile = capture.StagedFile ?? FindNewestFile(stagingDir);
            if (stagedFile is null || !File.Exists(stagedFile))
            {
                return Result(job, DownloadOutcome.Failed, null, capture.VideoId, capture.Extractor, exitCode,
                    "yt-dlp exited successfully but produced no output file.", startedAt);
            }

            var relativeName = Path.GetRelativePath(stagingDir, stagedFile);
            var publishedPath = await _publisher.PublishAsync(stagedFile, outputRoot, relativeName, ct).ConfigureAwait(false);
            return Result(job, DownloadOutcome.Success, publishedPath, capture.VideoId, capture.Extractor, exitCode, null, startedAt);
        }
        finally
        {
            TryCleanup(stagingDir);
        }
    }

    private static void HandleOutput(string line, IProgress<DownloadProgress>? progress, Capture capture)
    {
        if (YtDlpProgressParser.TryParse(line, out var update))
        {
            progress?.Report(update);
            return;
        }

        if (line.StartsWith(YtDlpArgBuilder.DoneMarker, StringComparison.Ordinal))
        {
            var fields = line[YtDlpArgBuilder.DoneMarker.Length..].Trim().Split('\t');
            if (fields.Length == 3)
            {
                capture.Extractor = fields[0];
                capture.VideoId = fields[1];
                capture.StagedFile = fields[2];
            }

            return;
        }

        if (line.Contains(AlreadyArchivedMarker, StringComparison.OrdinalIgnoreCase))
        {
            capture.AlreadyArchived = true;
        }
    }

    private static string? Tail(List<string> lines) =>
        lines.Count == 0 ? null : string.Join('\n', lines.TakeLast(ErrorTailLines));

    private static string? FindNewestFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static void TryCleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort; staging is swept again on the next run
        }
    }

    private static DownloadResult Result(
        DownloadJob job, DownloadOutcome outcome, string? publishedPath, string? videoId,
        string? extractor, int exitCode, string? error, long startedAt) =>
        new(job, outcome, publishedPath, videoId, extractor, exitCode, error, Stopwatch.GetElapsedTime(startedAt));

    private sealed class Capture
    {
        public bool AlreadyArchived { get; set; }
        public string? Extractor { get; set; }
        public string? VideoId { get; set; }
        public string? StagedFile { get; set; }
    }
}
