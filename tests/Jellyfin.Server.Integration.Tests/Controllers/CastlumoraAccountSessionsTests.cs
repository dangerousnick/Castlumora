using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraAccountSessionsTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraAccountSessionsTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PersonalSessions_AreBoundedTokenFreeAndCannotRevokeAnotherIdentityOrCurrentSignIn()
    {
        var token = TestContext.Current.CancellationToken;
        const string path = "Castlumora/Account/Sessions";
        const string password = "Owned-device-fixture-48!";
        var suffix = Guid.NewGuid().ToString("N");
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var createdIds = new List<string>();
        string? apiKey = null;
        try
        {
            var names = new[] { "DeviceOwner" + suffix, "DevicePeer" + suffix };
            foreach (var name in names)
            {
                using var created = await admin.PostAsJsonAsync("Users/New", new { Name = name, Password = password }, token);
                created.EnsureSuccessStatusCode();
                var user = await created.Content.ReadFromJsonAsync<JsonObject>(token);
                createdIds.Add(user!["Id"]!.GetValue<string>());
                Assert.False(user["Policy"]!["IsAdministrator"]!.GetValue<bool>());
            }

            async Task<string> Login(HttpClient client, string name, string device)
            {
                var prefix = "MediaBrowser Client=\"Owned device test\", Device=\"" + device + "\", DeviceId=\"" + device + suffix + "\", Version=\"1.0\"";
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, prefix);
                using var login = await client.PostAsJsonAsync("Users/AuthenticateByName", new { Username = name, Pw = password }, token);
                login.EnsureSuccessStatusCode();
                var authentication = await login.Content.ReadFromJsonAsync<JsonObject>(token);
                var secret = authentication!["AccessToken"]!.GetValue<string>();
                client.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, prefix + ", Token=\"" + secret + "\"");
                return secret;
            }

            using var owner = _factory.CreateClient();
            using var secondary = _factory.CreateClient();
            using var peer = _factory.CreateClient();
            var ownerToken = await Login(owner, names[0], "Current device");
            var secondaryToken = await Login(secondary, names[0], "Shared TV");
            var peerToken = await Login(peer, names[1], "Shared TV");
            using var anonymous = _factory.CreateClient();
            using var anonymousDenied = await anonymous.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousDenied.StatusCode);
            using var inventory = await owner.GetAsync(path + "?userId=" + createdIds[1], token);
            inventory.EnsureSuccessStatusCode();
            Assert.Contains("no-store", inventory.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
            var text = await inventory.Content.ReadAsStringAsync(token);
            foreach (var secret in new[] { ownerToken, secondaryToken, peerToken })
            {
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("AccessToken", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DeviceId", text, StringComparison.OrdinalIgnoreCase);
            var data = JsonNode.Parse(text)!;
            Assert.Equal(Guid.Parse(createdIds[0]), Guid.Parse(data["identityId"]!.GetValue<string>()));
            Assert.Equal(2, data["total"]!.GetValue<int>());
            var rows = data["sessions"]!.AsArray();
            var current = Assert.Single(rows, row => row!["current"]!.GetValue<bool>())!["id"]!.GetValue<int>();
            var other = Assert.Single(rows, row => !row!["current"]!.GetValue<bool>())!["id"]!.GetValue<int>();
            Assert.True(current > 0 && other > 0);
            var first = await owner.GetFromJsonAsync<JsonObject>(path + "?limit=1", token);
            var second = await owner.GetFromJsonAsync<JsonObject>(path + "?limit=1&startIndex=1", token);
            Assert.Single(first!["sessions"]!.AsArray());
            Assert.Single(second!["sessions"]!.AsArray());
            Assert.NotEqual(first["sessions"]![0]!["id"]!.GetValue<int>(), second["sessions"]![0]!["id"]!.GetValue<int>());
            using var oversized = await owner.GetAsync(path + "?limit=101", token);
            Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
            using var negative = await owner.GetAsync(path + "?startIndex=-1", token);
            Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
            var peerInventory = await peer.GetFromJsonAsync<JsonObject>(path, token);
            var foreignId = Assert.Single(peerInventory!["sessions"]!.AsArray())!["id"]!.GetValue<int>();
            using var foreign = await owner.DeleteAsync(path + "/" + foreignId, token);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            using var currentDenied = await owner.DeleteAsync(path + "/" + current, token);
            Assert.Equal(HttpStatusCode.Conflict, currentDenied.StatusCode);
            using var revoked = await owner.DeleteAsync(path + "/" + other, token);
            Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
            using var oldToken = await secondary.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);
            using var peerRetained = await peer.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, peerRetained.StatusCode);
            using var currentRetained = await owner.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, currentRetained.StatusCode);
            using var retry = await owner.DeleteAsync(path + "/" + other, token);
            Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
            using var another = _factory.CreateClient();
            await Login(another, names[0], "Another device");
            using var overrideDenied = await owner.PostAsJsonAsync(path + "/RevokeOthers", new { userId = createdIds[1] }, token);
            Assert.Equal(HttpStatusCode.BadRequest, overrideDenied.StatusCode);
            using var allOthers = await owner.PostAsJsonAsync(path + "/RevokeOthers", new { }, token);
            Assert.Equal(HttpStatusCode.NoContent, allOthers.StatusCode);
            using var allOthersRevoked = await another.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, allOthersRevoked.StatusCode);
            using var ownerStillValid = await owner.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, ownerStillValid.StatusCode);
            using var peerStillValid = await peer.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, peerStillValid.StatusCode);
            var final = await owner.GetFromJsonAsync<JsonObject>(path, token);
            Assert.True(Assert.Single(final!["sessions"]!.AsArray())!["current"]!.GetValue<bool>());
            Assert.Equal(1, final["total"]!.GetValue<int>());

            var app = "Owned session-key test " + suffix;
            using var issued = await admin.PostAsync("Auth/Keys?app=" + Uri.EscapeDataString(app), null, token);
            issued.EnsureSuccessStatusCode();
            var keys = await admin.GetFromJsonAsync<JsonObject>("Auth/Keys", token);
            apiKey = keys!["Items"]!.AsArray().Single(row => row!["AppName"]!.GetValue<string>() == app)!["AccessToken"]!.GetValue<string>();
            using var machine = _factory.CreateClient();
            machine.DefaultRequestHeaders.AddAuthHeader(apiKey);
            using var keyDenied = await machine.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.Forbidden, keyDenied.StatusCode);
            using var keyRevokeDenied = await machine.PostAsJsonAsync(path + "/RevokeOthers", new { }, token);
            Assert.Equal(HttpStatusCode.Forbidden, keyRevokeDenied.StatusCode);
            using var keyDeleteDenied = await machine.DeleteAsync(path + "/" + foreignId, token);
            Assert.Equal(HttpStatusCode.Forbidden, keyDeleteDenied.StatusCode);
        }
        finally
        {
            if (apiKey is not null)
            {
                using var removedKey = await admin.DeleteAsync("Auth/Keys/" + apiKey, token);
                removedKey.EnsureSuccessStatusCode();
            }

            foreach (var id in createdIds)
            {
                using var removedUser = await admin.DeleteAsync("Users/" + id, token);
                removedUser.EnsureSuccessStatusCode();
            }
        }
    }
}
