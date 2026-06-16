using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml.Controls;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.ViewModels;

/// <summary>
/// Drives the Browse page: turns a captured page (rendered DOM + network-observed stream URLs)
/// into persisted posts, and exports the WebView2 session cookies for yt-dlp.
/// </summary>
public partial class BrowseViewModel : ObservableObject
{
    private readonly IPostPageParser _parser;
    private readonly IArchiveRepository _repository;
    private readonly ICookieExporter _cookieExporter;
    private readonly CoreOptions _options;

    public BrowseViewModel(
        IPostPageParser parser,
        IArchiveRepository repository,
        ICookieExporter cookieExporter,
        IOptions<CoreOptions> options)
    {
        _parser = parser;
        _repository = repository;
        _cookieExporter = cookieExporter;
        _options = options.Value;
    }

    [ObservableProperty]
    public partial bool HasStatus { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial string StatusTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Parses a captured page, persists discovered posts, and reports how many are new.</summary>
    public async Task CaptureAsync(string html, string? pageTitle, IReadOnlyCollection<string> observedUrls)
    {
        var inventory = BuildInventory(html, observedUrls);
        if (inventory.VideoPostCount == 0)
        {
            SetStatus(InfoBarSeverity.Warning, "取得", "このページから動画投稿が見つかりませんでした。ログインとページの読み込みを確認してください。");
            return;
        }

        var host = inventory.Posts[0].Stream.Host;
        var handle = DeriveHandle(host, pageTitle);

        var creator = await _repository.GetOrCreateCreatorAsync(handle, host, null);
        var known = await _repository.GetKnownTokensAsync(creator.Id);
        var newCount = inventory.Posts.Count(p => !known.Contains(p.Token));
        await _repository.UpsertPostsAsync(creator.Id, inventory.Posts);

        SetStatus(
            InfoBarSeverity.Success,
            "取得完了",
            $"{handle}: {inventory.VideoPostCount} 件の投稿（うち新規 {newCount} 件）。Sync で取得できます。");
    }

    /// <summary>Writes the captured cookies to the configured Netscape cookies file.</summary>
    public async Task ExportCookiesAsync(IReadOnlyCollection<Cookie> cookies)
    {
        if (string.IsNullOrEmpty(_options.CookiesFilePath))
        {
            return;
        }

        await _cookieExporter.ExportAsync(_options.CookiesFilePath, cookies);
        SetStatus(InfoBarSeverity.Success, "Cookie", $"{cookies.Count} 件の Cookie を書き出しました（yt-dlp フォールバック認証用）。");
    }

    private InventoryResult BuildInventory(string html, IReadOnlyCollection<string> observedUrls)
    {
        var fromDom = _parser.Parse(html);
        var seen = fromDom.Posts.Select(p => p.Token).ToHashSet(StringComparer.Ordinal);

        // Network-observed token URLs are often script-injected and absent from the static DOM.
        var extras = observedUrls
            .SelectMany(StreamReference.Scan)
            .Where(stream => seen.Add(stream.Segment))
            .Select(stream => new Post
            {
                Id = 0,
                CreatorId = 0,
                Stream = stream,
                Title = Humanize(stream.Slug),
                Status = PostStatus.Discovered,
            });

        return new InventoryResult([.. fromDom.Posts, .. extras]);
    }

    private static string DeriveHandle(string host, string? pageTitle)
    {
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            var trimmed = pageTitle.Split('|', '–', '-')[0].Trim();
            trimmed = trimmed.Replace(" on Patreon", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (trimmed.Length is > 0 and <= 80)
            {
                return trimmed;
            }
        }

        // Fall back to the stream subdomain: "stream.acme.tv" -> "acme".
        var labels = host.Split('.');
        return labels.Length >= 2 && labels[0].Equals("stream", StringComparison.OrdinalIgnoreCase)
            ? labels[1]
            : host;
    }

    private static string Humanize(string slug)
    {
        var spaced = slug.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(spaced) ? slug : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }

    private void SetStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusSeverity = severity;
        StatusTitle = title;
        StatusMessage = message;
        HasStatus = true;
    }
}
