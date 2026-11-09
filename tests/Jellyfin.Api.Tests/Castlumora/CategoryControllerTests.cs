using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class CategoryControllerTests
{
    [Fact]
    public async Task ManualSelections_KeepAllowedLibraryScopeAndNeverFallBackToWholeCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), "castlumora-category-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(directory);
            using var store = new PresentationStore(paths.Object);
            var layout = PresentationStore.Defaults();
            layout["categories"]!.AsArray().Add(JsonNode.Parse("""{"id":"manual","title":"Manual","rules":{"itemIds":["11111111111111111111111111111111"],"folderIds":[],"genres":[],"studios":[]}}"""));
            var draft = await store.SaveLayoutAsync(layout, false);
            await store.SaveLayoutAsync(draft, true);
            var viewer = new User("Viewer", "auth", "reset");
            var users = new Mock<IUserManager>();
            users.Setup(u => u.GetUserById(viewer.Id)).Returns(viewer);
            var root = Guid.NewGuid();
            var library = new Mock<ILibraryManager>();
            library.Setup(l => l.ConfigureUserAccess(It.IsAny<InternalItemsQuery>(), viewer)).Callback<InternalItemsQuery, User>((query, _) => query.TopParentIds = [root]);
            library.Setup(l => l.GetItemIds(It.IsAny<InternalItemsQuery>())).Returns([]);
            var controller = new CastlumoraCategoriesController(store, library.Object, users.Object, Mock.Of<IDtoService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(InternalClaimTypes.UserId, viewer.Id.ToString())], "test"))
                    }
                }
            };
            var response = await controller.GetItems("manual");
            Assert.NotNull(response.Value);
            Assert.Empty(response.Value.Items);
            library.Verify(l => l.GetItemIds(It.Is<InternalItemsQuery>(q => q.TopParentIds.Length == 1 && q.TopParentIds[0].Equals(root) && q.ItemIds.Length == 1 && q.User == viewer)), Times.Once);
            library.Verify(l => l.GetItemsResult(It.IsAny<InternalItemsQuery>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
