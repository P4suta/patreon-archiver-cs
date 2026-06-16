using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using PatreonArchiver.App.Services;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;
using Windows.System;

namespace PatreonArchiver.App.ViewModels;

/// <summary>Backs the Settings page: preset, theme, engine versions, and storage locations.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly CoreOptions _options;
    private readonly IToolLocator _tools;

    public SettingsViewModel(IOptions<CoreOptions> options, IToolLocator tools, AppSettings settings)
    {
        _options = options.Value;
        _tools = tools;
        Settings = settings;
        UseFastPreset = settings.DefaultPreset == PresetKind.Fast;
        MinimumFreeSpaceGb = settings.MinimumFreeSpaceBytes / 1024d / 1024d / 1024d;
    }

    /// <summary>Shared, persisted settings (theme lives here so it survives page navigation).</summary>
    public AppSettings Settings { get; }

    public string DatabasePath => _options.DatabasePath;

    public string OutputRoot => _options.OutputRoot;

    public string CookiesPath => string.IsNullOrEmpty(_options.CookiesFilePath) ? "(未設定)" : _options.CookiesFilePath!;

    [ObservableProperty]
    public partial bool UseFastPreset { get; set; }

    [ObservableProperty]
    public partial double MinimumFreeSpaceGb { get; set; }

    [ObservableProperty]
    public partial string YtDlpVersion { get; set; } = "未確認";

    [ObservableProperty]
    public partial string FfmpegVersion { get; set; } = "未確認";

    partial void OnUseFastPresetChanged(bool value) =>
        Settings.DefaultPreset = value ? PresetKind.Fast : PresetKind.Polite;

    partial void OnMinimumFreeSpaceGbChanged(double value) =>
        Settings.MinimumFreeSpaceBytes = (long)(Math.Max(0, value) * 1024 * 1024 * 1024);

    [RelayCommand]
    private async Task OpenOutputFolderAsync()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        await Launcher.LaunchFolderPathAsync(_options.OutputRoot);
    }

    [RelayCommand]
    private async Task ProbeToolsAsync()
    {
        var paths = _tools.Resolve();
        YtDlpVersion = await ProbeAsync(paths.YtDlp, "--version");
        FfmpegVersion = await ProbeAsync(paths.Ffmpeg, "-version");
    }

    private static async Task<string> ProbeAsync(string exePath, string argument)
    {
        if (!File.Exists(exePath))
        {
            return "未導入";
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(exePath, argument)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Split('\n', 2)[0].Trim();
        }
        catch (Exception ex)
        {
            return $"確認失敗: {ex.Message}";
        }
    }
}
