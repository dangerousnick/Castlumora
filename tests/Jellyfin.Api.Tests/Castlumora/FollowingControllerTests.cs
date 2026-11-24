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
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class FollowingControllerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "following-" + Guid.NewGuid().ToString("N"));
    private readonly User _first = new("First", "auth", "reset");
    private readonly User _second = new("Second", "auth", "reset");
    private readonly Guid _series = Guid.NewGuid();
    private readonly Mock<IUserManager> _users = new();
    private readonly Mock<ILibraryManager> _library = new();
    private readonly PresentationStore _store;
    private bool _visible = true;

    public FollowingControllerTests()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(value => value.ConfigurationDirectoryPath).Returns(_root);
        _store = new PresentationStore(paths.Object);
        _users.Setup(users => users.GetUserById(_first.Id)).Returns(_first);
        _users.Setup(users => users.GetUserById(_second.Id)).Returns(_second);
        _library.Setup(library => library.GetItemById<Series>(It.IsAny<Guid>(), It.IsAny<User?>())).Returns((Guid id, User? user) =>
            _visible && id.Equals(_series) && user is not null ? new Series { Id = _series, Name = "Owned show" } : null);
    }

    [Fact]
    public async Task Membership_IsPrivatePersistentDeduplicatedAndFilteredWhenAccessChanges()
    {
        var first = Actor(_first);
        var second = Actor(_second);
        Assert.False((await first.Status(_series)).Value!["followed"]!.GetValue<bool>());
        Assert.True((await first.Set(_series, new JsonObject { ["enabled"] = true })).Value!["followed"]!.GetValue<bool>());
        await first.Set(_series, new JsonObject { ["enabled"] = true });
        Assert.Equal(1, (await first.List()).Value!["total"]!.GetValue<int>());
        Assert.Empty((await first.List(1)).Value!["seriesIds"]!.AsArray());
        Assert.False((await second.Status(_series)).Value!["followed"]!.GetValue<bool>());
        Assert.Equal(0, (await second.List()).Value!["total"]!.GetValue<int>());
        _visible = false;
        Assert.Equal(0, (await first.List()).Value!["total"]!.GetValue<int>());
        Assert.IsType<NotFoundResult>((await first.Status(_series)).Result);
        Assert.IsType<NotFoundResult>((await first.Set(_series, new JsonObject { ["enabled"] = true })).Result);
        Assert.False((await first.Set(_series, new JsonObject { ["enabled"] = false })).Value!["followed"]!.GetValue<bool>());
        _visible = true;
        Assert.False((await first.Status(_series)).Value!["followed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task InvalidTypesOverridesAndMachineCredentialsCannotMutateFollowing()
    {
        var first = Actor(_first);
        Assert.IsType<NotFoundResult>((await first.Set(Guid.NewGuid(), new JsonObject { ["enabled"] = true })).Result);
        Assert.IsType<BadRequestObjectResult>((await first.Set(_series, new JsonObject { ["enabled"] = true, ["userId"] = _second.Id.ToString("N") })).Result);
        Assert.IsType<BadRequestObjectResult>((await first.Set(_series, new JsonObject { ["enabled"] = "yes" })).Result);
        Assert.IsType<BadRequestObjectResult>((await first.Set(Guid.Empty, new JsonObject { ["enabled"] = true })).Result);
        var machine = Actor(_first, true);
        Assert.IsType<ForbidResult>((await machine.List()).Result);
        Assert.IsType<ForbidResult>((await machine.Status(_series)).Result);
        Assert.IsType<ForbidResult>((await machine.Set(_series, new JsonObject { ["enabled"] = true })).Result);
        Assert.IsType<ForbidResult>((await machine.Clear(new JsonObject())).Result);
        Assert.Equal(0, (await first.List()).Value!["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Clear_RemovesUnavailableReferencesWithoutChangingWatchlistOrOtherIdentity()
    {
        await _store.PreferencesAsync(_first.Id, "watchlist", _series, true);
        await Actor(_first).Set(_series, new JsonObject { ["enabled"] = true });
        await Actor(_second).Set(_series, new JsonObject { ["enabled"] = true });
        _visible = false;
        Assert.IsType<BadRequestObjectResult>((await Actor(_first).Clear(new JsonObject { ["accountId"] = _second.Id.ToString("N") })).Result);
        Assert.IsType<NoContentResult>((await Actor(_first).Clear(new JsonObject())).Result);
        _visible = true;
        Assert.Equal(0, (await Actor(_first).List()).Value!["total"]!.GetValue<int>());
        Assert.Equal(1, (await Actor(_second).List()).Value!["total"]!.GetValue<int>());
        Assert.Single((await _store.PreferencesAsync(_first.Id, "watchlist", null, false))["watchlist"]!.AsArray());
    }

    [Fact]
    public async Task CorruptStorage_NeverReportsSuccessfulMutation()
    {
        await Actor(_first).Set(_series, new JsonObject { ["enabled"] = true });
        var path = Path.Combine(_root, "castlumora", "user-" + _first.Id.ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "invalid json", TestContext.Current.CancellationToken);
        var result = Assert.IsType<ObjectResult>((await Actor(_first).Set(_series, new JsonObject { ["enabled"] = false })).Result);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("invalid json", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    private CastlumoraFollowingController Actor(User user, bool machine = false)
    {
        var claims = new List<Claim> { new(InternalClaimTypes.UserId, user.Id.ToString("N")), new(InternalClaimTypes.IsApiKey, machine.ToString()) };
        return new CastlumoraFollowingController(_store, _users.Object, _library.Object)
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
