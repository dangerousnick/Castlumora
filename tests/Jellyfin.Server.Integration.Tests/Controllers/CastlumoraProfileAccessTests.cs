using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraProfileAccessTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraProfileAccessTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AccessUpdate_RequiresAdministratorAndCurrentReview_RevokesOnlyChildAndPreservesIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var username = "AccessOwner" + suffix;
        const string password = "Profile-access-fixture-57!";
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var root = Path.Combine(Path.GetTempPath(), "profile-access-" + suffix);
        Directory.CreateDirectory(root);
        var clients = new List<HttpClient>();
        var children = new List<string>();
        string? ownerId = null;
        string? apiKey = null;
        try
        {
            using var createdOwner = await admin.PostAsJsonAsync("Users/New", new { Name = username, Password = password }, ct);
            createdOwner.EnsureSuccessStatusCode();
            ownerId = (await createdOwner.Content.ReadFromJsonAsync<JsonObject>(ct))!["Id"]!.GetValue<string>();
            var folders = new List<string>();
            for (var index = 0; index < 2; index++)
            {
                var directory = Path.Combine(root, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                var name = "Access" + suffix + index;
                using var added = await admin.PostAsJsonAsync("Library/VirtualFolders?name=" + name + "&collectionType=homevideos&paths=" + Uri.EscapeDataString(directory), new { }, ct);
                added.EnsureSuccessStatusCode();
                var list = await admin.GetFromJsonAsync<JsonArray>("Library/VirtualFolders", ct);
                folders.Add(list!.Single(row => row!["Name"]!.GetValue<string>() == name)!["ItemId"]!.GetValue<string>());
            }

            for (var index = 0; index < 2; index++)
            {
                using var created = await admin.PostAsJsonAsync("Castlumora/Profiles", new { accountId = ownerId, name = "Access " + index, preset = "kids", libraries = new[] { folders[0] } }, ct);
                created.EnsureSuccessStatusCode();
                children.Add((await created.Content.ReadFromJsonAsync<JsonObject>(ct))!["id"]!.GetValue<string>());
            }

            async Task<HttpClient> SignIn(string? profileId = null)
            {
                var client = _factory.CreateClient();
                clients.Add(client);
                var header = "MediaBrowser Client=\"Access fixture\", Device=\"Browser\", DeviceId=\"" + Guid.NewGuid().ToString("N") + "\", Version=\"1\"";
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header);
                using var signedIn = await client.PostAsJsonAsync("Users/AuthenticateByName", new { Username = username, Pw = password }, ct);
                signedIn.EnsureSuccessStatusCode();
                var secret = (await signedIn.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>();
                void SetToken(string value)
                {
                    client.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header + ", Token=\"" + value + "\"");
                }

                SetToken(secret);
                if (profileId is not null)
                {
                    using var switched = await client.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId, accountPassword = password }, ct);
                    switched.EnsureSuccessStatusCode();
                    SetToken((await switched.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>());
                }

                return client;
            }

            async Task<string> Revision()
            {
                var page = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/Managed", ct);
                Assert.True(page!["accessEditingAvailable"]!.GetValue<bool>());
                return page["profiles"]!.AsArray().Single(row => row!["id"]!.GetValue<string>() == children[0])!["accessRevision"]!.GetValue<string>();
            }

            var parent = await SignIn();
            var child = await SignIn(children[0]);
            var sibling = await SignIn(children[1]);
            var path = "Castlumora/Profiles/Managed/" + children[0] + "/Access";
            var revision = await Revision();
            var request = new { preset = "adult", libraries = new[] { folders[1] }, revision };
            var privateRoot = Path.Combine(_factory.Services.GetRequiredService<IApplicationPaths>().ConfigurationDirectoryPath, "castlumora", "profiles");
            var key = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), ct);
            var registry = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct);
            using var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(path, request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await parent.PutAsJsonAsync(path, request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await child.PutAsJsonAsync(path, request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("Castlumora/Profiles/Managed/" + ownerId + "/Access", request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { preset = "adult", libraries = new[] { Guid.NewGuid().ToString("N") }, revision }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { preset = "adult", libraries = new[] { folders[0], folders[0] }, revision }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { preset = "adult", libraries = new[] { folders[0] }, revision, accountId = ownerId }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { preset = "adult", libraries = new[] { 42 }, revision }, ct)).StatusCode);
            Assert.Equal(registry, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct));
            Assert.Equal(HttpStatusCode.OK, (await child.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await child.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await sibling.GetAsync("Users/Me", ct)).StatusCode);
            var user = await admin.GetFromJsonAsync<JsonObject>("Users/" + children[0], ct);
            Assert.Equal(Guid.Parse(children[0]), Guid.Parse(user!["Id"]!.GetValue<string>()));
            Assert.False(user["Policy"]!["IsAdministrator"]!.GetValue<bool>());
            Assert.Equal(Guid.Parse(folders[1]), Guid.Parse(Assert.Single(user["Policy"]!["EnabledFolders"]!.AsArray())!.GetValue<string>()));
            var fresh = await SignIn(children[0]);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, request, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("Users/Me", ct)).StatusCode);
            var reviewed = await Revision();
            var owner = await admin.GetFromJsonAsync<JsonObject>("Users/" + ownerId, ct);
            var ownerPolicy = owner!["Policy"]!.AsObject();
            ownerPolicy["EnableAllFolders"] = false;
            ownerPolicy["EnabledFolders"] = new JsonArray(folders[0]);
            (await admin.PostAsJsonAsync("Users/" + ownerId + "/Policy", ownerPolicy, ct)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new { preset = "kids", libraries = new[] { folders[0] }, revision = reviewed }, ct)).StatusCode);
            reviewed = await Revision();
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { preset = "adult", libraries = new[] { folders[1] }, revision = reviewed }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, new { preset = "kids", libraries = new[] { folders[0] }, revision = reviewed }, ct)).StatusCode);
            using var restored = await SignIn(children[0]);
            var restoredUser = await restored.GetFromJsonAsync<JsonObject>("Users/Me", ct);
            Assert.Equal(Guid.Parse(children[0]), Guid.Parse(restoredUser!["Id"]!.GetValue<string>()));
            Assert.Equal(6, restoredUser["Policy"]!["MaxParentalRating"]!.GetValue<int>());
            Assert.Equal(key, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), ct));
            Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await sibling.GetAsync("Users/Me", ct)).StatusCode);
            using var issue = await admin.PostAsync("Auth/Keys?app=AccessFixture" + suffix, null, ct);
            issue.EnsureSuccessStatusCode();
            var keys = await admin.GetFromJsonAsync<JsonObject>("Auth/Keys", ct);
            apiKey = keys!["Items"]!.AsArray().Single(row => row!["AppName"]!.GetValue<string>() == "AccessFixture" + suffix)!["AccessToken"]!.GetValue<string>();
            using var machine = _factory.CreateClient();
            machine.DefaultRequestHeaders.AddAuthHeader(apiKey);
            var machineRequest = new { preset = "adult", libraries = new[] { folders[0] }, revision = await Revision() };
            var beforeMachine = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PutAsJsonAsync(path, machineRequest, ct)).StatusCode);
            Assert.Equal(beforeMachine, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct));
            Assert.Equal(HttpStatusCode.OK, (await restored.GetAsync("Users/Me", ct)).StatusCode);
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

            foreach (var profile in children)
            {
                (await admin.DeleteAsync("Castlumora/Profiles/Managed/" + profile, ct)).EnsureSuccessStatusCode();
            }

            if (ownerId is not null)
            {
                (await admin.DeleteAsync("Users/" + ownerId, ct)).EnsureSuccessStatusCode();
            }

            Directory.Delete(root, true);
        }
    }
}
