using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PatreonArchiver.App.Services;

namespace PatreonArchiver.App.ViewModels;

/// <summary>Surfaces the shared <see cref="SyncCoordinator"/> state for the Downloads page.</summary>
public partial class DownloadsViewModel(SyncCoordinator coordinator) : ObservableObject
{
    public SyncCoordinator Coordinator { get; } = coordinator;

    [RelayCommand]
    private void Cancel() => Coordinator.Cancel();
}
