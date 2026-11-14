using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Controllers;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class SkipperServiceTests
{
    [Fact]
    public async Task ReviewedIntervals_PersistAndRejectStaleOrInvalidChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "castlumora-skipper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "episode.mp4");
            await File.WriteAllTextAsync(file, "synthetic local file", TestContext.Current.CancellationToken);
            var episode = new Episode { Id = Guid.NewGuid(), Path = file, RunTimeTicks = TimeSpan.FromMinutes(3).Ticks };
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(directory);
            var library = new Mock<ILibraryManager>();
            library.Setup(l => l.GetItemById(episode.Id)).Returns(episode);
            using var service = new SkipperService(paths.Object, library.Object, Mock.Of<IMediaEncoder>(), new Lazy<ISessionManager>(() => Mock.Of<ISessionManager>()));
            var state = JsonNode.Parse("""{"revision":0,"segments":[{"type":"Intro","status":"approved","startTicks":10000000,"endTicks":200000000},{"type":"Outro","status":"suggested","startTicks":1500000000,"endTicks":1700000000}]}""")!.AsObject();
            var saved = await service.SaveItemAsync(episode, state);
            Assert.Equal(1, saved["revision"]!.GetValue<int>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveItemAsync(episode, state));
            var returned = await service.GetMediaSegments(new MediaSegmentGenerationRequest { ItemId = episode.Id, ExistingSegments = [] }, TestContext.Current.CancellationToken);
            Assert.Single(returned);
            Assert.Equal(TimeSpan.FromSeconds(20).Ticks, returned[0].EndTicks);
            var invalid = saved.DeepClone().AsObject();
            invalid["segments"]![0]!["endTicks"] = long.MaxValue;
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveItemAsync(episode, invalid));
            var protectedContent = saved.DeepClone().AsObject();
            protectedContent["protectedFromTicks"] = TimeSpan.FromSeconds(160).Ticks;
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveItemAsync(episode, protectedContent));
            using var reopened = new SkipperService(paths.Object, library.Object, Mock.Of<IMediaEncoder>(), new Lazy<ISessionManager>(() => Mock.Of<ISessionManager>()));
            Assert.Equal(saved.ToJsonString(), (await reopened.ItemAsync(episode.Id)).ToJsonString());
            await File.AppendAllTextAsync(file, "modified media", TestContext.Current.CancellationToken);
            Assert.Empty(await reopened.GetMediaSegments(new MediaSegmentGenerationRequest { ItemId = episode.Id, ExistingSegments = [] }, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ProfileModes_AreIsolatedAndAnalysisDefaultsToOptIn()
    {
        var directory = Path.Combine(Path.GetTempPath(), "castlumora-skipper-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(directory);
            using var service = new SkipperService(paths.Object, Mock.Of<ILibraryManager>(), Mock.Of<IMediaEncoder>(), new Lazy<ISessionManager>(() => Mock.Of<ISessionManager>()));
            Assert.False((await service.SettingsAsync())["analysisEnabled"]!.GetValue<bool>());
            var first = Guid.NewGuid();
            await service.SaveSettingsAsync(new JsonObject { ["mode"] = "automatic" }, first);
            Assert.Equal("automatic", (await service.SettingsAsync(first))["mode"]!.GetValue<string>());
            Assert.Equal("default", (await service.SettingsAsync(Guid.NewGuid()))["mode"]!.GetValue<string>());
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveSettingsAsync(new JsonObject { ["mode"] = "unsafe" }, first));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Theory]
    [InlineData(nameof(CastlumoraSkipperController.SaveSettings))]
    [InlineData(nameof(CastlumoraSkipperController.GetItem))]
    [InlineData(nameof(CastlumoraSkipperController.SaveItem))]
    [InlineData(nameof(CastlumoraSkipperController.Analyze))]
    public void GlobalChangesAndAnalysis_RequireAdministrator(string method)
    {
        Assert.Contains(typeof(CastlumoraSkipperController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>(), a => a.Policy == Policies.RequiresElevation);
    }
}
