using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.App.ViewModels;

namespace PatreonArchiver.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SettingsViewModel>();
    }

    public SettingsViewModel ViewModel { get; }
}
