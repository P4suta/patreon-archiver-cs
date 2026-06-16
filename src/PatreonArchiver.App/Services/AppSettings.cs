using CommunityToolkit.Mvvm.ComponentModel;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.Services;

/// <summary>In-memory, observable user preferences shared across the app.</summary>
public sealed partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    public partial PresetKind DefaultPreset { get; set; } = PresetKind.Polite;

    /// <summary>0 = system, 1 = light, 2 = dark (default, dark-first).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; } = 2;

    /// <summary>Stop downloads when free disk space falls below this many bytes (default 20 GiB).</summary>
    [ObservableProperty]
    public partial long MinimumFreeSpaceBytes { get; set; } = SyncRequest.DefaultMinimumFreeSpaceBytes;
}
