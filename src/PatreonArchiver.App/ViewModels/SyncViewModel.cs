using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PatreonArchiver.App.Services;
using PatreonArchiver.Core.Abstractions;

namespace PatreonArchiver.App.ViewModels;

/// <summary>Lists creators with coverage/gap status and a per-creator "sync pending" trigger.</summary>
public partial class SyncViewModel(IArchiveRepository repository, SyncCoordinator coordinator) : ObservableObject
{
    public SyncCoordinator Coordinator { get; } = coordinator;

    public ObservableCollection<CreatorRowViewModel> Creators { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    public async Task LoadAsync()
    {
        Creators.Clear();
        foreach (var creator in await repository.GetCreatorsAsync())
        {
            var anchor = await repository.GetAnchorAsync(creator.Id);
            var pending = (await repository.GetDownloadablePostsAsync(creator.Id)).Count;
            Creators.Add(new CreatorRowViewModel(creator, anchor, pending, Coordinator, LoadAsync));
        }

        IsEmpty = Creators.Count == 0;
    }
}
