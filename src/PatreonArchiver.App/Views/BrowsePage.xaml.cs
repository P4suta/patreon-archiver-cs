using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using PatreonArchiver.App.ViewModels;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.Views;

/// <summary>
/// Hosts the embedded Patreon browser. Captures the rendered DOM (post cards), watches the network
/// for script-injected tokenized stream URLs, and exports the session cookies for yt-dlp.
/// </summary>
public sealed partial class BrowsePage : Page
{
    private readonly HashSet<string> _observedStreamUrls = new(StringComparer.Ordinal);

    public BrowsePage()
    {
        InitializeComponent();
        ViewModel = App.GetService<BrowseViewModel>();
    }

    public BrowseViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is not null)
        {
            return;
        }

        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PatreonArchiver", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
            string.Empty, userDataFolder, new CoreWebView2EnvironmentOptions());
        await Web.EnsureCoreWebView2Async(environment);

        var core = Web.CoreWebView2!;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;

        Web.Source = new Uri("https://www.patreon.com/login");
    }

    private void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        var uri = args.Request.Uri;
        if (uri.Contains("://stream.", StringComparison.OrdinalIgnoreCase))
        {
            _observedStreamUrls.Add(uri);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CanGoBack)
        {
            Web.GoBack();
        }
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CanGoForward)
        {
            Web.GoForward();
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Web.Reload();

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null)
        {
            return;
        }

        var html = Decode(await Web.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML"));
        var title = Decode(await Web.CoreWebView2.ExecuteScriptAsync("document.title"));
        await ViewModel.CaptureAsync(html ?? string.Empty, title, _observedStreamUrls.ToArray());
    }

    private async void ExportCookies_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null)
        {
            return;
        }

        var raw = await Web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.patreon.com");
        var cookies = raw
            .Select(c => new Cookie(
                c.Domain,
                c.Domain.StartsWith('.'),
                c.Path,
                c.IsSecure,
                c.IsSession ? 0 : (long)c.Expires,
                c.Name,
                c.Value))
            .ToList();

        await ViewModel.ExportCookiesAsync(cookies);
    }

    // ExecuteScriptAsync returns a JSON-encoded result; decode the string payload.
    private static string? Decode(string scriptResult) =>
        string.IsNullOrEmpty(scriptResult) || scriptResult == "null"
            ? null
            : JsonSerializer.Deserialize<string>(scriptResult);
}
