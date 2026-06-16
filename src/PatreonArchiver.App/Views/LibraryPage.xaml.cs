using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.App.ViewModels;

namespace PatreonArchiver.App.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<LibraryViewModel>();
    }

    public LibraryViewModel ViewModel { get; }

    public static Visibility VisibleIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();
}
