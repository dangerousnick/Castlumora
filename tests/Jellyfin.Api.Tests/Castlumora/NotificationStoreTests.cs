using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class NotificationStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "notification-state-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _series = Guid.NewGuid();
    private readonly Mock<IApplicationPaths> _paths = new();

    public NotificationStoreTests() => _paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(_root);

    [Fact]
    public async Task OptIn_BaselinesHistoryAndNewShows_Deduplicates_AndPersistsReadDismissal()
    {
        using var store = new PresentationStore(_paths.Object);
        var old = Guid.NewGuid();
        var added = Guid.NewGuid();
        EpisodeNotificationSnapshot[] Initial() => [new(_series, [old])];
        Assert.False((await store.NotificationsAsync(_user, Initial()))["enabled"]!.GetValue<bool>());
        Assert.Empty((await store.NotificationsAsync(_user, Initial(), enabled: true))["rows"]!.AsArray());
        EpisodeNotificationSnapshot[] Later() => [new(_series, [old, added]), new(Guid.NewGuid(), [Guid.NewGuid()])];
        var first = await store.NotificationsAsync(_user, Later());
        Assert.Single(first["rows"]!.AsArray());
        Assert.Equal(added.ToString("N"), first["rows"]![0]!["id"]!.GetValue<string>());
        await store.NotificationsAsync(_user, Initial(), episodeId: added, read: true);
        using var reopened = new PresentationStore(_paths.Object);
        Assert.True((await reopened.NotificationsAsync(_user, Initial()))["rows"]![0]!["read"]!.GetValue<bool>());
        await reopened.NotificationsAsync(_user, Initial(), episodeId: added, dismiss: true);
        var repeat = await reopened.NotificationsAsync(_user, [new(_series, [old, added])]);
        Assert.Single(repeat["rows"]!.AsArray());
        Assert.True(repeat["rows"]![0]!["dismissed"]!.GetValue<bool>());
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() => reopened.NotificationsAsync(_user, Initial(), episodeId: added, read: false));
    }

    [Fact]
    public async Task ProfilesAndOtherPreferencesAreIndependent_PauseResumeDoesNotReplayMissedHistory()
    {
        using var store = new PresentationStore(_paths.Object);
        var first = Guid.NewGuid();
        var paused = Guid.NewGuid();
        await store.PreferencesAsync(_user, "watchlist", first, true);
        await store.FollowingAsync(_user, _series, true);
        await store.NotificationsAsync(_user, [new(_series, [first])], enabled: true);
        await store.NotificationsAsync(_user, [new(_series, [first])], enabled: false);
        Assert.Empty((await store.NotificationsAsync(_user, [new(_series, [first, paused])]))["rows"]!.AsArray());
        Assert.Empty((await store.NotificationsAsync(_user, [new(_series, [first, paused])], enabled: true))["rows"]!.AsArray());
        var other = await store.NotificationsAsync(Guid.NewGuid(), [new(_series, [first, paused])]);
        Assert.False(other["enabled"]!.GetValue<bool>());
        Assert.Empty(other["rows"]!.AsArray());
        Assert.Single((await store.PreferencesAsync(_user, "watchlist", null, false))["watchlist"]!.AsArray());
        Assert.Single((await store.FollowingAsync(_user))["followedSeries"]!.AsArray());
    }

    [Fact]
    public async Task ConcurrentRefreshesDoNotDuplicate_UnknownUpdatesAndCorruptionDoNotOverwriteFiles()
    {
        using var store = new PresentationStore(_paths.Object);
        var episode = Guid.NewGuid();
        await store.NotificationsAsync(_user, [new(_series, [])], enabled: true);
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.NotificationsAsync(_user, [new(_series, [episode])])));
        Assert.Single((await store.NotificationsAsync(_user, [new(_series, [episode])]))["rows"]!.AsArray());
        var path = Path.Combine(_root, "castlumora", "notifications-" + _user.ToString("N") + ".json");
        var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() => store.NotificationsAsync(_user, [], episodeId: Guid.NewGuid(), read: true));
        Assert.Equal(saved, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":999}", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.NotificationsAsync(_user, [], enabled: true));
        Assert.Equal("{\"schemaVersion\":999}", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.NotificationsAsync(_user, [], enabled: true));
        Assert.Equal("{}", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        foreach (var malformed in new[] { "null", "[]" })
        {
            await File.WriteAllTextAsync(path, malformed, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.NotificationsAsync(_user, [], enabled: true));
            Assert.Equal(malformed, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task CapacityErrorsAreAtomic_OnlyNewest500RowsRetained_SeenEpisodesNeverReplay()
    {
        using var store = new PresentationStore(_paths.Object);
        await store.NotificationsAsync(_user, [new(_series, [])], enabled: true);
        var ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray();
        var state = await store.NotificationsAsync(_user, [new(_series, ids)]);
        Assert.Equal(500, state["rows"]!.AsArray().Count);
        Assert.DoesNotContain(state["rows"]!.AsArray(), n => n!["id"]!.GetValue<string>() == ids[0].ToString("N"));
        Assert.Equal(500, (await store.NotificationsAsync(_user, [new(_series, ids)]))["rows"]!.AsArray().Count);
        await Assert.ThrowsAsync<ArgumentException>(() => store.NotificationsAsync(_user, [new(_series, Enumerable.Range(0, 20001).Select(_ => Guid.NewGuid()).ToArray())]));
        Assert.Equal(500, (await store.NotificationsAsync(_user, [new(_series, ids)]))["rows"]!.AsArray().Count);
        await Assert.ThrowsAsync<ArgumentException>(() => store.NotificationsAsync(Guid.Empty, []));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
