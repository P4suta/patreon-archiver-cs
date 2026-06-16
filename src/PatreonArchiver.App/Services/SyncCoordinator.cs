using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.Services;

/// <summary>
/// Runs sync operations on a background thread and exposes observable progress for the UI. A single
/// shared instance so the Downloads page reflects whatever Browse/Sync kicked off. Progress is
/// marshaled to the UI thread via the app dispatcher.
/// </summary>
public sealed partial class SyncCoordinator : ObservableObject, IDisposable
{
    private readonly ISyncOrchestrator _orchestrator;
    private readonly IArchiveRepository _repository;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    public SyncCoordinator(ISyncOrchestrator orchestrator, IArchiveRepository repository, AppSettings settings)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _settings = settings;
    }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusLine { get; set; } = "アイドル";

    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>Newest-first log of completed runs.</summary>
    public ObservableCollection<string> Log { get; } = [];

    public void Cancel() => _cts?.Cancel();

    /// <summary>Syncs a creator using its already-known posts as the inventory (downloads what's pending).</summary>
    public async Task RunCreatorAsync(Creator creator)
    {
        if (IsRunning)
        {
            return;
        }

        var posts = await _repository.GetPostsAsync(creator.Id);
        await RunAsync(new SyncRequest
        {
            Handle = creator.Handle,
            StreamHost = creator.StreamHost,
            PatreonUrl = creator.PatreonUrl,
            Inventory = new InventoryResult(posts),
            Preset = _settings.DefaultPreset,
            MinimumFreeSpaceBytes = _settings.MinimumFreeSpaceBytes,
        });
    }

    public async Task RunAsync(SyncRequest request)
    {
        if (IsRunning)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsRunning = true;
        Progress = 0;
        StatusLine = $"{request.Handle} を同期中…";

        // Constructed on the UI thread, so reports marshal back to the UI thread.
        var progress = new Progress<SyncProgress>(OnProgress);

        try
        {
            var result = await Task.Run(() => _orchestrator.SyncAsync(request, progress, _cts.Token));
            if (result.DiskStop is { } disk)
            {
                var message = $"{request.Handle}: ディスク容量不足で停止（空き {Gb(disk.AvailableBytes):0.0} GB / 必要 {Gb(disk.RequiredBytes):0.0} GB）。空きを確保して再度 Sync してください。";
                StatusLine = message;
                Log.Insert(0, message);
            }
            else
            {
                var summary = $"{request.Handle}: DL {result.Downloaded} / 新規 {result.New} / スキップ {result.Skipped} / 失敗 {result.Failed}"
                    + (result.GapPending ? "（ギャップ保留）" : string.Empty);
                StatusLine = $"完了 — {summary}";
                Log.Insert(0, summary);
            }

            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            StatusLine = "キャンセルされました";
        }
        catch (Exception ex)
        {
            StatusLine = $"エラー: {ex.Message}";
            Log.Insert(0, $"{request.Handle}: エラー — {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void Dispose() => _cts?.Dispose();

    private static double Gb(long bytes) => bytes / 1024d / 1024d / 1024d;

    private void OnProgress(SyncProgress progress)
    {
        Progress = progress.Total == 0 ? 0 : (double)progress.Completed / progress.Total * 100;
        var percent = progress.Download?.Percent ?? 0;
        StatusLine = progress.Current is { } post
            ? $"{post.Title} ({progress.Completed}/{progress.Total}) — {percent:0}%"
            : StatusLine;
    }
}
