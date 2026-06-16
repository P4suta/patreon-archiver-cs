using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Domain;

namespace PatreonArchiver.App.ViewModels;

/// <summary>Loads published videos across all creators for the library grid.</summary>
public partial class LibraryViewModel(IArchiveRepository repository) : ObservableObject
{
    public ObservableCollection<LibraryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    public async Task LoadAsync()
    {
        Items.Clear();
        foreach (var creator in await repository.GetCreatorsAsync())
        {
            foreach (var post in await repository.GetPostsAsync(creator.Id))
            {
                if (post.Status == PostStatus.Published)
                {
                    Items.Add(new LibraryItemViewModel(post, creator.DisplayName ?? creator.Handle));
                }
            }
        }

        IsEmpty = Items.Count == 0;
    }
}
