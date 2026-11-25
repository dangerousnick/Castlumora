using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraFollowingTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraFollowingTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Following_UsesNativePermissionsPrivateProfileStateAndPersistentPreferences()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var username = "FollowingOwner" + suffix;
        const string password = "Following-fixture-48!";
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var root = Path.Combine(Path.GetTempPath(), "following-http-" + suffix);
        Directory.CreateDirectory(root);
        string? ownerId = null;
        string? childId = null;
        string? apiKey = null;
        var clients = new List<HttpClient>();
        try
        {
            var folders = new List<string>();
            for (var index = 0; index < 2; index++)
            {
                var path = Path.Combine(root, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(path);
                var name = "Following" + suffix + index;
                using var added = await admin.PostAsJsonAsync("Library/VirtualFolders?name=" + name + "&collectionType=tvshows&paths=" + Uri.EscapeDataString(path), new { }, ct);
                added.EnsureSuccessStatusCode();
                var list = await admin.GetFromJsonAsync<JsonArray>("Library/VirtualFolders", ct);
                folders.Add(list!.Single(row => row!["Name"]!.GetValue<string>() == name)!["ItemId"]!.GetValue<string>());
            }

            var library = _factory.Services.GetRequiredService<ILibraryManager>();
            await library.ValidateTopLibraryFolders(ct);
            var collection = library.GetItemById<CollectionFolder>(Guid.Parse(folders[0]));
            Assert.NotNull(collection);
            // Register a real indexed physical root through native persistence; the empty-directory
            // test server does not run a media scan. Browser tests exercise actual scanning.
            var parent = new Folder { Id = Guid.NewGuid(), Name = "Notification physical fixture", Path = Path.Combine(root, "0") };
            parent.SetParent(library.RootFolder);
            library.CreateItem(parent, library.RootFolder);
            collection.PhysicalFolderIds = [parent.Id];
            await library.UpdateItemAsync(collection, library.GetUserRootFolder(), ItemUpdateType.MetadataEdit, ct);
            Assert.NotNull(parent);
            var series = new Series { Id = Guid.NewGuid(), Name = "Owned following series", Path = Path.Combine(root, "0", "Owned show"), OfficialRating = "G" };
            Directory.CreateDirectory(series.Path);
            series.SetParent(parent);
            library.CreateItem(series, parent);
            var movie = new Movie { Id = Guid.NewGuid(), Name = "Wrong type", Path = Path.Combine(root, "0", "movie.mp4"), OfficialRating = "G" };
            movie.SetParent(parent);
            library.CreateItem(movie, parent);
            using var createdOwner = await admin.PostAsJsonAsync("Users/New", new { Name = username, Password = password }, ct);
            createdOwner.EnsureSuccessStatusCode();
            ownerId = (await createdOwner.Content.ReadFromJsonAsync<JsonObject>(ct))!["Id"]!.GetValue<string>();
            using var createdProfile = await admin.PostAsJsonAsync("Castlumora/Profiles", new { accountId = ownerId, name = "Following kids", preset = "kids", libraries = new[] { folders[0] } }, ct);
            createdProfile.EnsureSuccessStatusCode();
            childId = (await createdProfile.Content.ReadFromJsonAsync<JsonObject>(ct))!["id"]!.GetValue<string>();
            async Task<HttpClient> SignIn(bool child)
            {
                var client = _factory.CreateClient();
                clients.Add(client);
                var header = "MediaBrowser Client=\"Following fixture\", Device=\"Browser\", DeviceId=\"" + Guid.NewGuid().ToString("N") + "\", Version=\"1\"";
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header);
                using var login = await client.PostAsJsonAsync("Users/AuthenticateByName", new { Username = username, Pw = password }, ct);
                login.EnsureSuccessStatusCode();
                var token = (await login.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>();
                void SetToken(string secret)
                {
                    client.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header + ", Token=\"" + secret + "\"");
                }

                SetToken(token);
                if (child)
                {
                    using var switched = await client.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = childId, accountPassword = password }, ct);
                    switched.EnsureSuccessStatusCode();
                    SetToken((await switched.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>());
                }

                return client;
            }

            var owner = await SignIn(false);
            var child = await SignIn(true);
            var target = "Castlumora/Following/" + series.Id.ToString("N");
            using var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("Castlumora/Following", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("Castlumora/Following?limit=101", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync("Castlumora/Following/" + movie.Id.ToString("N"), new { enabled = true }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(target, new { enabled = true, userId = childId }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync("Castlumora/Presentation/Preferences/followedSeries/" + series.Id.ToString("N") + "?enabled=true", null, ct)).StatusCode);
            (await owner.PutAsync("Castlumora/Presentation/Preferences/watchlist/" + movie.Id.ToString("N") + "?enabled=true", null, ct)).EnsureSuccessStatusCode();
            (await owner.PostAsync("UserPlayedItems/" + movie.Id.ToString("N"), null, ct)).EnsureSuccessStatusCode();
            var before = (await owner.GetFromJsonAsync<JsonObject>("Items/" + movie.Id.ToString("N"), ct))!["UserData"]!.ToJsonString();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.True((await (await owner.PutAsJsonAsync(target, new { enabled = true }, ct)).Content.ReadFromJsonAsync<JsonObject>(ct))!["followed"]!.GetValue<bool>());
            }

            var page = await owner.GetFromJsonAsync<JsonObject>("Castlumora/Following", ct);
            Assert.Equal(1, page!["total"]!.GetValue<int>());
            Assert.Single(page["seriesIds"]!.AsArray());
            Assert.False((await child.GetFromJsonAsync<JsonObject>(target, ct))!["followed"]!.GetValue<bool>());
            (await child.PutAsJsonAsync(target, new { enabled = true }, ct)).EnsureSuccessStatusCode();
            using var reopened = new PresentationStore(_factory.Services.GetRequiredService<IApplicationPaths>());
            Assert.Single((await reopened.FollowingAsync(Guid.Parse(ownerId)))["followedSeries"]!.AsArray());
            Assert.Single((await reopened.FollowingAsync(Guid.Parse(childId)))["followedSeries"]!.AsArray());
            Episode AddEpisode(string title, int number)
            {
                var episode = new Episode { Id = Guid.NewGuid(), Name = title, Path = Path.Combine(series.Path, title + ".mp4"), SeriesId = series.Id, SeriesName = series.Name, ParentIndexNumber = 1, IndexNumber = number, OfficialRating = "G" };
                episode.SetParent(series);
                library.CreateItem(episode, series);
                return episode;
            }

            _ = AddEpisode("Historic notification fixture", 1);
            const string notices = "Castlumora/Notifications";
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(notices, ct)).StatusCode);
            Assert.False((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["enabled"]!.GetValue<bool>());
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(notices + "/Settings", new { enabled = true, userId = childId }, ct)).StatusCode);
            (await owner.PutAsJsonAsync(notices + "/Settings", new { enabled = true }, ct)).EnsureSuccessStatusCode();
            Assert.Empty((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            var arrival = AddEpisode("New notification fixture", 2);
            var notificationPath = notices + "/" + arrival.Id.ToString("N");
            var notifications = await owner.GetFromJsonAsync<JsonObject>(notices, ct);
            Assert.Single(notifications!["items"]!.AsArray());
            Assert.Equal(arrival.Id.ToString("N"), notifications["items"]![0]!["id"]!.GetValue<string>());
            Assert.Equal(1, notifications["unread"]!.GetValue<int>());
            Assert.Single((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            Assert.Empty((await child.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            Assert.Equal(HttpStatusCode.NotFound, (await child.PutAsJsonAsync(notificationPath, new { read = true }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(notificationPath, new { read = true, accountId = childId }, ct)).StatusCode);
            (await owner.PutAsJsonAsync(notificationPath, new { read = true }, ct)).EnsureSuccessStatusCode();
            Assert.Equal(0, (await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["unread"]!.GetValue<int>());
            Assert.True((await reopened.NotificationsAsync(Guid.Parse(ownerId), []))["rows"]![0]!["read"]!.GetValue<bool>());
            var nativeOwner = await admin.GetFromJsonAsync<JsonObject>("Users/" + ownerId, ct);
            var policy = nativeOwner!["Policy"]!.AsObject();
            policy["EnableAllFolders"] = false;
            policy["EnabledFolders"] = new JsonArray(folders[1]);
            (await admin.PostAsJsonAsync("Users/" + ownerId + "/Policy", policy, ct)).EnsureSuccessStatusCode();
            Assert.Equal(0, (await owner.GetFromJsonAsync<JsonObject>("Castlumora/Following", ct))!["total"]!.GetValue<int>());
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(target, ct)).StatusCode);
            Assert.Empty((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            Assert.Equal(0, (await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["unread"]!.GetValue<int>());
            Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(notificationPath, new { read = false }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(target, new { enabled = true }, ct)).StatusCode);
            Assert.False((await (await owner.PutAsJsonAsync(target, new { enabled = false }, ct)).Content.ReadFromJsonAsync<JsonObject>(ct))!["followed"]!.GetValue<bool>());
            policy["EnableAllFolders"] = true;
            (await admin.PostAsJsonAsync("Users/" + ownerId + "/Policy", policy, ct)).EnsureSuccessStatusCode();
            (await owner.PutAsJsonAsync(target, new { enabled = true }, ct)).EnsureSuccessStatusCode();
            Assert.Single((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            (await owner.PutAsJsonAsync(notificationPath, new { dismissed = true }, ct)).EnsureSuccessStatusCode();
            Assert.Empty((await owner.GetFromJsonAsync<JsonObject>(notices, ct))!["items"]!.AsArray());
            Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(notificationPath, new { read = false }, ct)).StatusCode);
            (await owner.PutAsJsonAsync(target, new { enabled = false }, ct)).EnsureSuccessStatusCode();
            Assert.True((await child.GetFromJsonAsync<JsonObject>(target, ct))!["followed"]!.GetValue<bool>());
            Assert.False((await owner.GetFromJsonAsync<JsonObject>(target, ct))!["followed"]!.GetValue<bool>());
            (await owner.PutAsJsonAsync(target, new { enabled = true }, ct)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("Castlumora/Following/Clear", new { userId = childId }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("Castlumora/Following/Clear", new { }, ct)).StatusCode);
            Assert.True((await child.GetFromJsonAsync<JsonObject>(target, ct))!["followed"]!.GetValue<bool>());
            Assert.Equal(before, (await owner.GetFromJsonAsync<JsonObject>("Items/" + movie.Id.ToString("N"), ct))!["UserData"]!.ToJsonString());
            Assert.Single((await reopened.PreferencesAsync(Guid.Parse(ownerId), "watchlist", null, false))["watchlist"]!.AsArray());
            using var issued = await admin.PostAsync("Auth/Keys?app=Following" + suffix, null, ct);
            issued.EnsureSuccessStatusCode();
            var keys = await admin.GetFromJsonAsync<JsonObject>("Auth/Keys", ct);
            apiKey = keys!["Items"]!.AsArray().Single(row => row!["AppName"]!.GetValue<string>() == "Following" + suffix)!["AccessToken"]!.GetValue<string>();
            using var machine = _factory.CreateClient();
            machine.DefaultRequestHeaders.AddAuthHeader(apiKey);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.GetAsync("Castlumora/Following", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.GetAsync(notices, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PutAsJsonAsync(notices + "/Settings", new { enabled = true }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PutAsJsonAsync(notificationPath, new { read = true }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PutAsJsonAsync(target, new { enabled = true }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PostAsJsonAsync("Castlumora/Following/Clear", new { }, ct)).StatusCode);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }

            if (apiKey is not null)
            {
                (await admin.DeleteAsync("Auth/Keys/" + apiKey, ct)).EnsureSuccessStatusCode();
            }

            if (childId is not null)
            {
                (await admin.DeleteAsync("Castlumora/Profiles/Managed/" + childId, ct)).EnsureSuccessStatusCode();
            }

            if (ownerId is not null)
            {
                (await admin.DeleteAsync("Users/" + ownerId, ct)).EnsureSuccessStatusCode();
            }

            Directory.Delete(root, true);
        }
    }
}
