using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.App.ViewModels;

namespace PatreonArchiver.App.Views;

public sealed partial class DownloadsPage : Page
{
    public DownloadsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<DownloadsViewModel>();
    }

    public DownloadsViewModel ViewModel { get; }
}
