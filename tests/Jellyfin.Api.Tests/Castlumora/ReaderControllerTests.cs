using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ReaderControllerTests
{
    [Fact]
    public async Task MissingIdentityAndInaccessibleOrNonBookItemsCannotReadState()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(Path.GetTempPath());
        using var store = new ReaderStore(paths.Object);
        var library = new Mock<ILibraryManager>();
        var controller = Create(store, library.Object, Guid.Empty);
        var book = Guid.NewGuid();
        Assert.IsType<UnauthorizedResult>((await controller.GetState(book)).Result);
        library.Verify(l => l.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        var user = Guid.NewGuid();
        controller = Create(store, library.Object, user);
        Assert.IsType<NotFoundResult>((await controller.GetState(book)).Result);
        library.Verify(l => l.GetItemById<BaseItem>(book, user), Times.Once);
        library.Setup(l => l.GetItemById<BaseItem>(book, user)).Returns(new Folder());
        Assert.IsType<NotFoundResult>((await controller.GetState(book)).Result);
    }

    [Fact]
    public async Task SubmittedUserIdCannotChangeStateOwner()
    {
        var directory = Path.Combine(Path.GetTempPath(), "castlumora-reader-controller-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(directory);
            using var store = new ReaderStore(paths.Object);
            var library = new Mock<ILibraryManager>();
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var book = Guid.NewGuid();
            library.Setup(l => l.GetItemById<BaseItem>(book, owner)).Returns(new Book());
            var controller = Create(store, library.Object, owner);
            var value = new JsonObject { ["format"] = "pdf", ["location"] = "2", ["userId"] = other.ToString() };
            Assert.Equal("2", (await controller.SaveState(book, value)).Value!["location"]!.GetValue<string>());
            Assert.Equal(string.Empty, (await store.StateAsync(other, book))["location"]!.GetValue<string>());
            Assert.Equal("2", (await store.StateAsync(owner, book))["location"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static CastlumoraReaderController Create(ReaderStore store, ILibraryManager library, Guid user)
    {
        return new CastlumoraReaderController(store, library)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(user.Equals(Guid.Empty) ? Array.Empty<Claim>() : [new Claim(InternalClaimTypes.UserId, user.ToString())], "test"))
                }
            }
        };
    }
}
