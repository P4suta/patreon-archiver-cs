using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PatreonArchiver.Core.Domain;
using Windows.Storage;
using Windows.System;

namespace PatreonArchiver.App.ViewModels;

/// <summary>A single archived video in the library grid.</summary>
public partial class LibraryItemViewModel : ObservableObject
{
    public LibraryItemViewModel(Post post, string uploader)
    {
        Title = string.IsNullOrWhiteSpace(post.Title) ? post.Stream.Slug : post.Title!;
        Subtitle = $"{uploader} · {post.Date:yyyy-MM-dd}";
        FilePath = post.FilePath;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string? FilePath { get; }

    private bool HasFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task PlayAsync()
    {
        var file = await StorageFile.GetFileFromPathAsync(FilePath!);
        await Launcher.LaunchFileAsync(file);
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task OpenFolderAsync()
    {
        var folder = Path.GetDirectoryName(FilePath!);
        if (!string.IsNullOrEmpty(folder))
        {
            await Launcher.LaunchFolderPathAsync(folder);
        }
    }
}
