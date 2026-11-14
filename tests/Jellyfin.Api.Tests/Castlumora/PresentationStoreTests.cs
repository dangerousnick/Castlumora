using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Controllers;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class PresentationStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "castlumora-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DraftPublish_PersistsAndRejectsStaleWriters()
    {
        var store = CreateStore();
        var initial = await store.ReadLayoutAsync(false);
        var config = PresentationStore.Defaults();
        config["theme"]!["accent"] = "#abcdef";
        var saved = await store.SaveLayoutAsync(config, false);
        Assert.Equal(initial.ToJsonString(), (await store.ReadLayoutAsync(false)).ToJsonString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveLayoutAsync(config, false));
        var published = await store.SaveLayoutAsync(saved, true);
        var reloaded = await CreateStore().ReadLayoutAsync(false);
        Assert.Equal(published.ToJsonString(), reloaded.ToJsonString());
        Assert.Equal("#abcdef", reloaded["theme"]!["accent"]!.GetValue<string>());
    }

    [Fact]
    public async Task PublishedHistory_RestoresDraftWithoutChangingPublication()
    {
        using var store = CreateStore();
        var first = await store.SaveLayoutAsync(PresentationStore.Defaults(), false);
        first = await store.SaveLayoutAsync(first, true);
        var second = first.DeepClone().AsObject();
        second["theme"]!["accent"] = "#abcdef";
        second = await store.SaveLayoutAsync(second, false);
        second = await store.SaveLayoutAsync(second, true);
        Assert.Equal(2, (await CreateStore().HistoryAsync()).Count);
        var restored = await store.RestoreAsync(first["revision"]!.GetValue<int>(), second["revision"]!.GetValue<int>());
        Assert.Equal("#ad70ff", restored["theme"]!["accent"]!.GetValue<string>());
        Assert.Equal("#abcdef", (await store.ReadLayoutAsync(false))["theme"]!["accent"]!.GetValue<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RestoreAsync(2, 0));
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() => store.RestoreAsync(999, restored["revision"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Preferences_AreIsolatedAndIndependent()
    {
        var store = CreateStore();
        var user = Guid.NewGuid();
        var item = Guid.NewGuid();
        await store.PreferencesAsync(user, "hiddenResume", item, true);
        var state = await CreateStore().PreferencesAsync(user, "watchlist", null, false);
        Assert.Empty(state["watchlist"]!.AsArray());
        Assert.Single(state["hiddenResume"]!.AsArray());
        Assert.Empty((await store.PreferencesAsync(Guid.NewGuid(), "hiddenResume", null, false))["hiddenResume"]!.AsArray());
        await store.PreferencesAsync(user, "hiddenResume", item, false);
        Assert.Empty((await store.PreferencesAsync(user, "hiddenResume", null, false))["hiddenResume"]!.AsArray());
    }

    [Fact]
    public void Visibility_RemovesRestrictedCategoriesAndRows()
    {
        var layout = PresentationStore.Defaults();
        layout["categories"]!.AsArray().Add(JsonNode.Parse("""{"id":"private","title":"Private","userIds":["11111111111111111111111111111111"],"rules":{"itemIds":[],"folderIds":[],"genres":[],"studios":[]}}"""));
        layout["rows"]!.AsArray().Add(JsonNode.Parse("""{"id":"private-row","title":"Private","kind":"category","categoryId":"private"}"""));
        var visible = PresentationStore.ForUser(layout, Guid.NewGuid());
        Assert.Empty(visible["categories"]!.AsArray());
        Assert.DoesNotContain(visible["rows"]!.AsArray(), r => r!["id"]!.GetValue<string>() == "private-row");
        Assert.Single(layout["categories"]!.AsArray());
    }

    [Fact]
    public void Validation_RejectsUnsafeThemeAndInvalidReferences()
    {
        var layout = PresentationStore.Defaults();
        layout["theme"]!["accent"] = "url(javascript:alert(1))";
        Assert.Throws<ArgumentException>(() => PresentationStore.Validate(layout));
        layout = PresentationStore.Defaults();
        layout["rows"]!.AsArray().Add(JsonNode.Parse("""{"id":"bad","title":"Bad","kind":"category","categoryId":"missing"}"""));
        Assert.Throws<ArgumentException>(() => PresentationStore.Validate(layout));
    }

    [Theory]
    [InlineData(nameof(CastlumoraPresentationController.GetDraft))]
    [InlineData(nameof(CastlumoraPresentationController.SaveDraft))]
    [InlineData(nameof(CastlumoraPresentationController.Publish))]
    [InlineData(nameof(CastlumoraPresentationController.GetHistory))]
    [InlineData(nameof(CastlumoraPresentationController.Restore))]
    public void GlobalWrites_RequireExistingAdministratorPolicy(string method)
    {
        var attributes = typeof(CastlumoraPresentationController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Contains(attributes, a => a.Policy == Policies.RequiresElevation);
        Assert.NotEmpty(typeof(CastlumoraPresentationController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private PresentationStore CreateStore()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(_directory);
        return new PresentationStore(paths.Object);
    }
}
