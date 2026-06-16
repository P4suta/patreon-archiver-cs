using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PatreonArchiver.App.Services;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.ViewModels;

/// <summary>A creator row on the Sync page: coverage status, pending count, and a sync trigger.</summary>
public partial class CreatorRowViewModel : ObservableObject
{
    private readonly Creator _creator;
    private readonly SyncCoordinator _coordinator;
    private readonly Func<Task> _reload;

    public CreatorRowViewModel(Creator creator, CoverageAnchor anchor, int pending, SyncCoordinator coordinator, Func<Task> reload)
    {
        _creator = creator;
        _coordinator = coordinator;
        _reload = reload;

        Handle = creator.DisplayName ?? creator.Handle;
        CoverageText = anchor.AnchorDate is { } date ? $"カバレッジ: {date:yyyy-MM-dd} まで" : "カバレッジ: 未設定";
        HasGap = anchor.HasGap;
        GapText = anchor.HasGap
            ? $"⚠ ギャップ保留 ({anchor.PendingGapFrom:yyyy-MM-dd} → {anchor.PendingGapTo:yyyy-MM-dd})"
            : string.Empty;
        PendingText = pending == 0 ? "保留なし" : $"保留 {pending} 件";
    }

    public string Handle { get; }

    public string CoverageText { get; }

    public bool HasGap { get; }

    public string GapText { get; }

    public string PendingText { get; }

    [RelayCommand]
    private async Task SyncAsync()
    {
        await _coordinator.RunCreatorAsync(_creator);
        await _reload();
    }
}
