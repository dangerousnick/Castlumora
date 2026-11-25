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

public sealed class CastlumoraOwnedProfileSessionsTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraOwnedProfileSessionsTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task OwnerControls_RejectChildForeignAndMachineCallers_AndPreserveOwnerSiblingAndRegistry()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        const string password = "Owned-profile-session-test-57!";
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var people = new List<string>();
        var children = new List<string>();
        var clients = new List<HttpClient>();
        var folder = Path.Combine(Path.GetTempPath(), "owned-profile-sessions-" + suffix);
        Directory.CreateDirectory(folder);
        string? apiKey = null;
        try
        {
            var names = new[] { "Owner" + suffix, "Peer" + suffix };
            foreach (var name in names)
            {
                using var response = await admin.PostAsJsonAsync("Users/New", new { Name = name, Password = password }, ct);
                response.EnsureSuccessStatusCode();
                var user = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
                Assert.False(user!["Policy"]!["IsAdministrator"]!.GetValue<bool>());
                people.Add(user["Id"]!.GetValue<string>());
            }

            using var library = await admin.PostAsJsonAsync("Library/VirtualFolders?name=OwnedSessions" + suffix + "&collectionType=homevideos&paths=" + Uri.EscapeDataString(folder), new { }, ct);
            library.EnsureSuccessStatusCode();
            var libraries = await admin.GetFromJsonAsync<JsonArray>("Library/VirtualFolders", ct);
            var libraryId = libraries!.Single(row => row!["Name"]!.GetValue<string>() == "OwnedSessions" + suffix)!["ItemId"]!.GetValue<string>();
            foreach (var owner in new[] { people[0], people[0], people[1] })
            {
                using var response = await admin.PostAsJsonAsync("Castlumora/Profiles", new { accountId = owner, name = "Viewing " + children.Count, preset = "adult", libraries = new[] { libraryId } }, ct);
                response.EnsureSuccessStatusCode();
                children.Add((await response.Content.ReadFromJsonAsync<JsonObject>(ct))!["id"]!.GetValue<string>());
            }

            async Task<HttpClient> SignIn(string name, string? profile = null)
            {
                var client = _factory.CreateClient();
                clients.Add(client);
                var header = "MediaBrowser Client=\"Owned profile test\", Device=\"TV\", DeviceId=\"" + Guid.NewGuid().ToString("N") + "\", Version=\"1\"";
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header);
                using var login = await client.PostAsJsonAsync("Users/AuthenticateByName", new { Username = name, Pw = password }, ct);
                login.EnsureSuccessStatusCode();
                var secret = (await login.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>();
                void SetToken(string value)
                {
                    client.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, header + ", Token=\"" + value + "\"");
                }

                SetToken(secret);
                if (profile is not null)
                {
                    using var switched = await client.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = profile, accountPassword = password }, ct);
                    switched.EnsureSuccessStatusCode();
                    SetToken((await switched.Content.ReadFromJsonAsync<JsonObject>(ct))!["AccessToken"]!.GetValue<string>());
                }

                return client;
            }

            var ownerClient = await SignIn(names[0]);
            var peer = await SignIn(names[1]);
            var first = await SignIn(names[0], children[0]);
            var second = await SignIn(names[0], children[0]);
            var sibling = await SignIn(names[0], children[1]);
            var foreignChild = await SignIn(names[1], children[2]);
            string Route(string id) => "Castlumora/Account/ProfileSessions/" + id;
            var path = Route(children[0]);
            using var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync(path, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await first.PostAsJsonAsync(path + "/RevokeAll", new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await peer.GetAsync(path, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync(Route(children[2]), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.PostAsJsonAsync(Route(children[2]) + "/RevokeAll", new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync(Route(people[0]), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.GetAsync(path + "?limit=101", ct)).StatusCode);
            using var inventory = await ownerClient.GetAsync(path, ct);
            inventory.EnsureSuccessStatusCode();
            Assert.Contains("no-store", inventory.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
            var text = await inventory.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain("AccessToken", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DeviceId", text, StringComparison.OrdinalIgnoreCase);
            var page = JsonNode.Parse(text)!;
            Assert.Equal("Viewing 0", page["profileName"]!.GetValue<string>());
            var rows = page["sessions"]!.AsArray();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.False(row!["current"]!.GetValue<bool>()));
            var self = await first.GetFromJsonAsync<JsonObject>("Castlumora/Account/Sessions", ct);
            var record = Assert.Single(self!["sessions"]!.AsArray(), row => row!["current"]!.GetValue<bool>())!["id"]!.GetValue<int>();
            var ownerPage = await ownerClient.GetFromJsonAsync<JsonObject>("Castlumora/Account/Sessions", ct);
            var ownerRecord = Assert.Single(ownerPage!["sessions"]!.AsArray(), row => row!["current"]!.GetValue<bool>())!["id"]!.GetValue<int>();
            Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.DeleteAsync(path + "/" + ownerRecord, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await second.DeleteAsync(path + "/" + record, ct)).StatusCode);
            var privateRoot = Path.Combine(_factory.Services.GetRequiredService<IApplicationPaths>().ConfigurationDirectoryPath, "castlumora", "profiles");
            var registry = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct);
            var key = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), ct);
            Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.DeleteAsync(path + "/" + record, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync(path + "/RevokeAll", new { accountId = people[1] }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.PostAsJsonAsync(path + "/RevokeAll", new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("Users/Me", ct)).StatusCode);
            foreach (var retained in new[] { ownerClient, peer, sibling, foreignChild })
            {
                Assert.Equal(HttpStatusCode.OK, (await retained.GetAsync("Users/Me", ct)).StatusCode);
            }

            Assert.Equal(registry, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct));
            Assert.Equal(key, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), ct));
            Assert.Equal(0, (await ownerClient.GetFromJsonAsync<JsonObject>(path, ct))!["total"]!.GetValue<int>());
            const string accountRoute = "Castlumora/Account/ProfileSessions/RevokeOthers";
            var otherOwnerDevice = await SignIn(names[0]);
            var newChildDevice = await SignIn(names[0], children[0]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(accountRoute, new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await sibling.PostAsJsonAsync(accountRoute, new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync(accountRoute, new { accountId = people[1] }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await otherOwnerDevice.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await newChildDevice.GetAsync("Users/Me", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.PostAsJsonAsync(accountRoute, new { }, ct)).StatusCode);
            foreach (var revoked in new[] { otherOwnerDevice, newChildDevice, sibling })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await revoked.GetAsync("Users/Me", ct)).StatusCode);
            }

            foreach (var retained in new[] { ownerClient, peer, foreignChild })
            {
                Assert.Equal(HttpStatusCode.OK, (await retained.GetAsync("Users/Me", ct)).StatusCode);
            }

            Assert.Equal(registry, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), ct));
            Assert.Equal(key, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), ct));
            using var issue = await admin.PostAsync("Auth/Keys?app=OwnedProfile" + suffix, null, ct);
            issue.EnsureSuccessStatusCode();
            var keys = await admin.GetFromJsonAsync<JsonObject>("Auth/Keys", ct);
            apiKey = keys!["Items"]!.AsArray().Single(row => row!["AppName"]!.GetValue<string>() == "OwnedProfile" + suffix)!["AccessToken"]!.GetValue<string>();
            using var machine = _factory.CreateClient();
            machine.DefaultRequestHeaders.AddAuthHeader(apiKey);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.GetAsync(path, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.DeleteAsync(path + "/" + record, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PostAsJsonAsync(path + "/RevokeAll", new { }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await machine.PostAsJsonAsync(accountRoute, new { }, ct)).StatusCode);
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

            foreach (var child in children)
            {
                (await admin.DeleteAsync("Castlumora/Profiles/Managed/" + child, ct)).EnsureSuccessStatusCode();
            }

            foreach (var person in people)
            {
                (await admin.DeleteAsync("Users/" + person, ct)).EnsureSuccessStatusCode();
            }

            Directory.Delete(folder, true);
        }
    }
}
