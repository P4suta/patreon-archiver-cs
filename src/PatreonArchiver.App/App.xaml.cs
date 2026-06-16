using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PatreonArchiver.App.Services;
using PatreonArchiver.App.ViewModels;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.DependencyInjection;

namespace PatreonArchiver.App;

/// <summary>
/// Application entry point. Builds the generic-host service container (wiring up the headless
/// <c>PatreonArchiver.Core</c> services), initializes the database, and shows the shell window.
/// </summary>
public partial class App : Application
{
    private static readonly IHost AppHost = BuildHost();

    public App() => InitializeComponent();

    /// <summary>The shell window. Used for dialogs, pickers, and WinRT interop.</summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>The UI-thread dispatcher, captured at launch for marshaling background work.</summary>
    public static DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>The native window handle (HWND) for file pickers and interop.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>Resolves a registered service.</summary>
    public static T GetService<T>() where T : notnull => AppHost.Services.GetRequiredService<T>();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Apply database migrations before any page can query the repository.
        await GetService<IArchiveRepository>().InitializeAsync();

        Window = new MainWindow();
        Window.Activate();
    }

    private static IHost BuildHost()
    {
        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PatreonArchiver");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPatreonArchiverCore(options =>
        {
            options.DatabasePath = Path.Combine(dataRoot, "state.db");
            options.StagingRoot = Path.Combine(dataRoot, "staging");
            options.OutputRoot = Path.Combine(dataRoot, "library");
            options.CookiesFilePath = Path.Combine(dataRoot, "cookies.txt");
        });

        builder.Services.AddSingleton<AppSettings>();
        builder.Services.AddSingleton<SyncCoordinator>();
        builder.Services.AddTransient<BrowseViewModel>();
        builder.Services.AddTransient<DownloadsViewModel>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<SyncViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        return builder.Build();
    }
}
