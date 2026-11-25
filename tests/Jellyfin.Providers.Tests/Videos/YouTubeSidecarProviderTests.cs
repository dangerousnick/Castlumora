using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Providers.Videos;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.Videos;

public sealed class YouTubeSidecarProviderTests : IDisposable
{
    private const string Id = "AbCdEf123_-";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-sidecar-" + Guid.NewGuid().ToString("N"));
    private readonly LibraryOptions _options = new();
    private readonly YouTubeSidecarProvider _provider;

    public YouTubeSidecarProviderTests()
    {
        Directory.CreateDirectory(_root);
        var library = new Mock<ILibraryManager>();
        library.Setup(l => l.GetLibraryOptions(It.IsAny<BaseItem>())).Returns(_options);
        _provider = new YouTubeSidecarProvider(library.Object, NullLogger<YouTubeSidecarProvider>.Instance);
    }

    [Fact]
    public async Task RetainedCreatorIdentity_IsNotA_FuzzyMovieMatch()
    {
        var info = await Write(JsonSerializer.Serialize(new
        {
            id = Id,
            extractor_key = "Youtube",
            webpage_url = "https://www.youtube.com/watch?v=" + Id,
            title = "Unspeakable",
            description = "A creator video <script>unsafe()</script>",
            channel_id = "UC" + new string('a', 22),
            channel = "Known Creator",
            upload_date = "20240312",
            duration = 99999999,
            thumbnail = "http://127.0.0.1/never-request-this",
            _filename = "../../never-write-this",
        }));
        var result = await Read(info);
        Assert.True(result.HasMetadata);
        Assert.True(result.QueriedById);
        Assert.IsType<Video>(result.Item);
        Assert.Equal("Unspeakable", result.Item.Name);
        Assert.Equal(Id, result.Item.ProviderIds["YouTube"]);
        Assert.Equal("UC" + new string('a', 22), result.Item.ProviderIds["YouTubeChannel"]);
        Assert.Equal(["Known Creator"], result.Item.Studios);
        Assert.Contains("&lt;script&gt;", result.Item.Overview, StringComparison.Ordinal);
        Assert.Equal(new DateTime(2024, 3, 12, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate);
        Assert.Null(result.Item.RunTimeTicks);
        Assert.Empty(result.RemoteImages);
        Assert.False(result.Item.IsLocked);
        Assert.Equal(["CreatorVideo", "YouTube"], result.Item.Tags);
        Assert.Equal(1, _provider.Order);
    }

    [Theory]
    [InlineData("{broken json")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"AbCdEf123_-\",\"title\":\"MrBeast\"}")]
    [InlineData("{\"id\":\"AbCdEf123_-\",\"extractor\":\"vimeo\",\"title\":\"MrBeast\"}")]
    [InlineData("{\"id\":\"../../bad\",\"extractor\":\"youtube\",\"title\":\"MrBeast\"}")]
    [InlineData("{\"id\":\"AbCdEf123_-\",\"id\":\"ZYXWV987654\",\"extractor\":\"youtube\",\"title\":\"MrBeast\"}")]
    [InlineData("{\"id\":\"AbCdEf123_-\",\"extractor\":\"youtube\",\"title\":\"\"}")]
    public async Task MalformedOrAmbiguousIdentity_ReturnsNoGuess(string json)
    {
        Assert.False((await Read(await Write(json))).HasMetadata);
    }

    [Theory]
    [InlineData("http://127.0.0.1/internal")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://www.youtube.com.evil.test/watch?v=AbCdEf123_-")]
    [InlineData("https://attacker@www.youtube.com/watch?v=AbCdEf123_-")]
    [InlineData("https://www.youtube.com:444/watch?v=AbCdEf123_-")]
    [InlineData("https://www.youtube.com/watch?v=ZYXWV987654")]
    public async Task ConflictingOrUnsafeSourceReference_ReturnsNoMatch(string url)
    {
        var info = await Write(JsonSerializer.Serialize(new { id = Id, extractor = "youtube", title = "MrBeast", webpage_url = url }));
        Assert.False((await Read(info)).HasMetadata);
    }

    [Fact]
    public async Task ExplicitMovieType_FilenameConflict_AndDisabledReader_AreRespected()
    {
        var info = await Write(ValidJson());
        info.ItemType = typeof(Movie);
        Assert.False((await Read(info)).HasMetadata);
        info.ItemType = typeof(Video);
        _options.DisabledLocalMetadataReaders = [_provider.Name];
        Assert.False((await Read(info)).HasMetadata);
        _options.DisabledLocalMetadataReaders = [];
        Assert.False((await Read(await Write(ValidJson(), "MrBeast [ZYXWV987654].mp4"))).HasMetadata);
        Assert.True((await Read(info)).HasMetadata);
    }

    [Fact]
    public async Task BoundedReads_MissingFiles_AndCancellation_AreHandled()
    {
        var info = await Write(new string(' ', (2 * 1024 * 1024) + 1));
        Assert.False((await Read(info)).HasMetadata);
        File.Delete(Path.ChangeExtension(info.Path, ".info.json"));
        Assert.False((await Read(info)).HasMetadata);
        await Write(ValidJson());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.GetMetadata(info, Mock.Of<IDirectoryService>(), cancellation.Token));
    }

    [Fact]
    public async Task SidecarChangeIsObserved_AndSymlinkIsNotFollowed()
    {
        var info = await Write(ValidJson());
        var video = new Video { Path = info.Path, DateLastSaved = DateTime.UtcNow.AddMinutes(-1) };
        Assert.True(_provider.HasChanged(video, Mock.Of<IDirectoryService>()));
        video.DateLastSaved = DateTime.UtcNow.AddMinutes(1);
        Assert.False(_provider.HasChanged(video, Mock.Of<IDirectoryService>()));
        if (!OperatingSystem.IsWindows())
        {
            var path = Path.ChangeExtension(info.Path, ".info.json");
            File.Move(path, path + ".retained");
            File.CreateSymbolicLink(path, path + ".retained");
            Assert.False((await Read(info)).HasMetadata);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static string ValidJson() => JsonSerializer.Serialize(new { id = Id, extractor_key = "Youtube", title = "MrBeast" });

    private async Task<ItemInfo> Write(string json, string name = "local-video.mp4")
    {
        var path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".info.json"), json, TestContext.Current.CancellationToken);
        return new ItemInfo(new Video { Path = path });
    }

    private Task<MetadataResult<Video>> Read(ItemInfo info)
        => _provider.GetMetadata(info, Mock.Of<IDirectoryService>(), TestContext.Current.CancellationToken);
}
