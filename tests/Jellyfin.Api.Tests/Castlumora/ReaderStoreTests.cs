using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ReaderStoreTests
{
    [Fact]
    public async Task Positions_AreProfileAndBookIsolatedAndPersistAcrossInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "castlumora-reader-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(directory);
            var user = Guid.NewGuid();
            var book = Guid.NewGuid();
            using var store = new ReaderStore(paths.Object);
            var value = new JsonObject { ["format"] = "pdf", ["location"] = "7", ["theme"] = "sepia", ["fontSize"] = 20 };
            await store.StateAsync(user, book, value);
            using var reopened = new ReaderStore(paths.Object);
            Assert.Equal("7", (await reopened.StateAsync(user, book))["location"]!.GetValue<string>());
            Assert.Equal(string.Empty, (await reopened.StateAsync(Guid.NewGuid(), book))["location"]!.GetValue<string>());
            Assert.Equal(string.Empty, (await reopened.StateAsync(user, Guid.NewGuid()))["location"]!.GetValue<string>());
            value["location"] = "-1";
            await Assert.ThrowsAsync<ArgumentException>(() => store.StateAsync(user, book, value));
            await Assert.ThrowsAsync<ArgumentException>(() => store.StateAsync(Guid.Empty, book));
            value["format"] = "epub";
            value["location"] = "epubcfi(/6/2!/4/2:0)";
            await store.StateAsync(user, book, value);
            value["location"] = "https://untrusted.example/";
            await Assert.ThrowsAsync<ArgumentException>(() => store.StateAsync(user, book, value));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
