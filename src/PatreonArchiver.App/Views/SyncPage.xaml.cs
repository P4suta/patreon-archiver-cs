using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.App.ViewModels;

namespace PatreonArchiver.App.Views;

public sealed partial class SyncPage : Page
{
    public SyncPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SyncViewModel>();
    }

    public SyncViewModel ViewModel { get; }

    public static Visibility VisibleIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();
}
