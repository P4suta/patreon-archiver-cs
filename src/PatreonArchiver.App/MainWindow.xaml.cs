using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.App.Services;
using PatreonArchiver.App.Views;
using Windows.Graphics;

namespace PatreonArchiver.App;

/// <summary>
/// The shell window: a Mica title bar plus a left <see cref="NavigationView"/> whose content frame
/// hosts the feature pages.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Pages = new(StringComparer.Ordinal)
    {
        ["Browse"] = typeof(BrowsePage),
        ["Library"] = typeof(LibraryPage),
        ["Downloads"] = typeof(DownloadsPage),
        ["Sync"] = typeof(SyncPage),
    };

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1280, 840));

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(BrowsePage));

        var settings = App.GetService<AppSettings>();
        ApplyTheme(settings.ThemeIndex);
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.ThemeIndex))
            {
                ApplyTheme(settings.ThemeIndex);
            }
        };
    }

    private void ApplyTheme(int themeIndex) => RootGrid.RequestedTheme = themeIndex switch
    {
        1 => ElementTheme.Light,
        2 => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string tag } && Pages.TryGetValue(tag, out var page))
        {
            ContentFrame.Navigate(page);
        }
    }
}
