using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class NotificationControllerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "notification-controller-" + Guid.NewGuid().ToString("N"));
    private readonly User _user = new("Viewer", "auth", "reset");
    private readonly Guid _series = Guid.NewGuid();
    private readonly Mock<IUserManager> _users = new();
    private readonly Mock<ILibraryManager> _library = new();
    private readonly List<BaseItem> _episodes = [];
    private readonly PresentationStore _store;

    public NotificationControllerTests()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(_root);
        _store = new PresentationStore(paths.Object);
        _users.Setup(u => u.GetUserById(_user.Id)).Returns(_user);
        _users.Setup(u => u.GetUserById(Guid.Empty)).Throws<ArgumentException>();
        _library.Setup(l => l.GetItemById<Series>(_series, _user)).Returns(new Series { Id = _series, Name = "Visible series" });
        _library.Setup(l => l.GetItemList(It.IsAny<InternalItemsQuery>())).Returns(_episodes);
        _library.Setup(l => l.GetItemById<Episode>(It.IsAny<Guid>(), _user)).Returns((Guid id, User? _) => _episodes.Find(e => e.Id.Equals(id)) as Episode);
    }

    [Fact]
    public async Task PausedReadsAndUpdatesNeverScanCatalog_ButRetainVisibleReadState()
    {
        await _store.FollowingAsync(_user.Id, _series, true);
        var actor = Actor();
        Assert.False((await actor.List()).Value!["enabled"]!.GetValue<bool>());
        _library.Verify(l => l.GetItemList(It.IsAny<InternalItemsQuery>()), Times.Never);
        await actor.Settings(new JsonObject { ["enabled"] = true });
        var episode = new Episode { Id = Guid.NewGuid(), SeriesId = _series, Name = "Arrival" };
        _episodes.Add(episode);
        Assert.Single((await actor.List()).Value!["items"]!.AsArray());
        _library.Invocations.Clear();
        Assert.Single((await actor.Settings(new JsonObject { ["enabled"] = false })).Value!["items"]!.AsArray());
        Assert.Equal(0, (await actor.Update(episode.Id, new JsonObject { ["read"] = true })).Value!["unread"]!.GetValue<int>());
        Assert.Single((await actor.List()).Value!["items"]!.AsArray());
        _library.Verify(l => l.GetItemList(It.IsAny<InternalItemsQuery>()), Times.Never);
    }

    [Fact]
    public async Task ApiKeysAreRejectedBeforeEmptyUserLookup_AndCorruptionDoesNotResetState()
    {
        var machine = Actor(true);
        Assert.IsType<ForbidResult>((await machine.List()).Result);
        Assert.IsType<ForbidResult>((await machine.Settings(new JsonObject { ["enabled"] = true })).Result);
        Assert.IsType<ForbidResult>((await machine.Update(Guid.NewGuid(), new JsonObject { ["read"] = true })).Result);
        _users.Verify(u => u.GetUserById(Guid.Empty), Times.Never);
        var actor = Actor();
        Assert.IsType<BadRequestObjectResult>((await actor.Settings(new JsonObject { ["enabled"] = true, ["userId"] = Guid.NewGuid().ToString() })).Result);
        Assert.IsType<BadRequestObjectResult>((await actor.Update(Guid.NewGuid(), new JsonObject { ["dismissed"] = false })).Result);
        await actor.Settings(new JsonObject { ["enabled"] = true });
        var path = Path.Combine(_root, "castlumora", "notifications-" + _user.Id.ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "invalid-json", TestContext.Current.CancellationToken);
        Assert.Equal(503, Assert.IsType<ObjectResult>((await actor.List()).Result).StatusCode);
        Assert.Equal("invalid-json", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    private CastlumoraNotificationsController Actor(bool machine = false)
    {
        var claims = new List<Claim> { new(InternalClaimTypes.UserId, (machine ? Guid.Empty : _user.Id).ToString("N")), new(InternalClaimTypes.IsApiKey, machine.ToString()) };
        return new CastlumoraNotificationsController(_store, _users.Object, _library.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")) } }
        };
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
