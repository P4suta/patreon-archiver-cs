using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.Core.Sync;

/// <summary>
/// Orchestrates a sync, faithfully porting <c>sync.py</c>'s ordering: filter what's new, evaluate
/// the coverage anchor, download the visible diff, and advance the anchor only after a fully clean
/// run with no open gap.
/// </summary>
internal sealed class SyncOrchestrator(
    IArchiveRepository repository,
    IUrlResolver resolver,
    IDownloadEngine engine,
    IBatchThrottle throttle,
    IDiskSpaceGuard diskSpaceGuard,
    IClock clock) : ISyncOrchestrator
{
    private static readonly IReadOnlySet<string> NoTokens = new HashSet<string>();

    public async Task<SyncResult> SyncAsync(
        SyncRequest request, IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var inventory = request.Inventory;

        // Read-only projection first, so a dry run never creates or mutates any state.
        var existing = await repository.FindCreatorAsync(request.Handle, ct).ConfigureAwait(false);
        var known = existing is null
            ? NoTokens
            : await repository.GetKnownTokensAsync(existing.Id, ct).ConfigureAwait(false);
        var newCount = inventory.Posts.Count(p => !known.Contains(p.Token));

        var anchor = existing is null
            ? CoverageAnchor.None(0, clock.UtcNow)
            : await repository.GetAnchorAsync(existing.Id, ct).ConfigureAwait(false);
        var decision = CoverageEvaluator.Evaluate(anchor.AnchorDate, inventory.OldestDate, inventory.NewestDate);

        if (request.DryRun)
        {
            return new SyncResult(
                inventory.VideoPostCount, newCount, 0, 0, 0, anchor, decision.HasGap, decision.GapWarning, []);
        }

        var creator = await repository.GetOrCreateCreatorAsync(
            request.Handle, request.StreamHost, request.PatreonUrl, ct).ConfigureAwait(false);
        await repository.UpsertPostsAsync(creator.Id, inventory.Posts, ct).ConfigureAwait(false);

        if (decision.HasGap && anchor.AnchorDate is { } gapFrom && inventory.OldestDate is { } gapTo)
        {
            await repository.SetGapAsync(creator.Id, gapFrom, gapTo, ct).ConfigureAwait(false);
        }

        var archiveFile = await repository.ExportArchiveFileAsync(creator.Id, ct).ConfigureAwait(false);
        var downloadable = await repository.GetDownloadablePostsAsync(creator.Id, ct).ConfigureAwait(false);

        var results = new List<DownloadResult>(downloadable.Count);
        var allSucceeded = true;
        var completed = 0;
        DiskSpaceStatus? diskStop = null;

        foreach (var post in downloadable)
        {
            ct.ThrowIfCancellationRequested();

            // Stop before starting a download that could fill the disk (free space shrinks as we go).
            var disk = diskSpaceGuard.Check(request.MinimumFreeSpaceBytes);
            if (!disk.HasHeadroom)
            {
                diskStop = disk;
                break;
            }

            var resolved = await resolver.ResolveAsync(post.Stream.Url, ct).ConfigureAwait(false);
            var job = BuildJob(creator, post, resolved, request, archiveFile);

            var jobProgress = progress is null
                ? null
                : new DelegateProgress<DownloadProgress>(p =>
                    progress.Report(new SyncProgress(completed, downloadable.Count, post, p)));

            var result = await engine.DownloadAsync(job, jobProgress, ct).ConfigureAwait(false);
            results.Add(result);
            await repository.AddHistoryAsync(result, post.Id, ct).ConfigureAwait(false);

            if (result.Outcome == DownloadOutcome.Cancelled)
            {
                allSucceeded = false;
                break;
            }

            await ApplyOutcomeAsync(repository, creator.Id, post.Id, result, ct).ConfigureAwait(false);
            allSucceeded &= result.Outcome != DownloadOutcome.Failed;

            completed++;
            progress?.Report(new SyncProgress(completed, downloadable.Count, post, null));
            await throttle.WaitAsync(request.Preset, ct).ConfigureAwait(false);
        }

        // Advance the anchor only after a fully clean, complete run with no pending gap or disk stop.
        if (allSucceeded && diskStop is null && !decision.HasGap && decision.ProposedAnchor is { } advance)
        {
            await repository.AdvanceAnchorAsync(creator.Id, advance, ct).ConfigureAwait(false);
        }

        var finalAnchor = await repository.GetAnchorAsync(creator.Id, ct).ConfigureAwait(false);
        return new SyncResult(
            inventory.VideoPostCount,
            newCount,
            results.Count(r => r.Outcome == DownloadOutcome.Success),
            results.Count(r => r.Outcome == DownloadOutcome.AlreadyArchived),
            results.Count(r => r.Outcome == DownloadOutcome.Failed),
            finalAnchor,
            decision.HasGap,
            decision.GapWarning,
            results)
        {
            DiskStop = diskStop,
        };
    }

    private static DownloadJob BuildJob(Creator creator, Post post, Uri resolved, SyncRequest request, string archiveFile) =>
        new()
        {
            SourceUrl = resolved.ToString(),
            Preset = request.Preset,
            Metadata = new PostMetadata
            {
                Uploader = creator.Handle,
                Title = post.Title,
                Date = post.Date,
                PostUrl = post.PatreonPostUrl,
            },
            CookiesFilePath = request.CookiesFilePath,
            ArchiveFile = archiveFile,
            PostId = post.Id,
        };

    private static async Task ApplyOutcomeAsync(
        IArchiveRepository repository, long creatorId, long postId, DownloadResult result, CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case DownloadOutcome.Success:
                await repository.MarkPostAsync(postId, PostStatus.Published, result.PublishedPath, result.VideoId, ct).ConfigureAwait(false);
                if (result.Extractor is { } extractor && result.VideoId is { } videoId)
                {
                    await repository.RecordArchiveAsync(creatorId, extractor.ToLowerInvariant(), videoId, ct).ConfigureAwait(false);
                }

                break;

            case DownloadOutcome.AlreadyArchived:
                await repository.MarkPostAsync(postId, PostStatus.Skipped, null, result.VideoId, ct).ConfigureAwait(false);
                break;

            default:
                await repository.MarkPostAsync(postId, PostStatus.Failed, null, result.VideoId, ct).ConfigureAwait(false);
                break;
        }
    }
}
