using PatreonArchiver.Core.Publishing;

namespace PatreonArchiver.Core.Tests.Publishing;

public sealed class AtomicPublisherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pa-pub-{Guid.NewGuid():N}");
    private readonly AtomicPublisher _publisher = new();

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public async Task Publishes_into_a_relative_subpath_and_creates_directories()
    {
        var staged = StageFile("video-bytes");
        var dest = Path.Combine(_root, "out");

        var published = await _publisher.PublishAsync(staged, dest, Path.Combine("acme", "2026-01-15_ep.mp4"));

        Assert.True(File.Exists(published));
        Assert.Equal("video-bytes", await File.ReadAllTextAsync(published));
        Assert.EndsWith(Path.Combine("acme", "2026-01-15_ep.mp4"), published);
    }

    [Fact]
    public async Task Leaves_no_temp_file_behind()
    {
        var staged = StageFile("data");
        var dest = Path.Combine(_root, "out");

        await _publisher.PublishAsync(staged, dest, "file.mp4");

        Assert.Empty(Directory.EnumerateFiles(dest, ".pa-publish.*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Overwrites_an_existing_destination()
    {
        var dest = Path.Combine(_root, "out");

        await _publisher.PublishAsync(StageFile("first"), dest, "file.mp4");
        var published = await _publisher.PublishAsync(StageFile("second"), dest, "file.mp4");

        Assert.Equal("second", await File.ReadAllTextAsync(published));
    }

    private string StageFile(string content)
    {
        var staging = Path.Combine(_root, "stage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var path = Path.Combine(staging, "staged.mp4");
        File.WriteAllText(path, content);
        return path;
    }
}
