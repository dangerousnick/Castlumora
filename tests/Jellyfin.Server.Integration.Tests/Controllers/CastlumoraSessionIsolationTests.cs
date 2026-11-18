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

public sealed class CastlumoraSessionIsolationTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraSessionIsolationTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Logout_PreservesOtherAccountsAndApplicationsOnTheSameClaimedDevice()
    {
        var token = TestContext.Current.CancellationToken;
        using var administrator = _factory.CreateClient();
        administrator.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(administrator));
        var createdIds = new List<string>();
        var suffix = Guid.NewGuid().ToString("N");
        var deviceId = "shared-device-" + suffix;
        const string password = "Owned-session-fixture-43!";
        try
        {
            var names = new[] { "SessionOwner" + suffix, "SessionPeer" + suffix };
            foreach (var name in names)
            {
                using var created = await administrator.PostAsJsonAsync("Users/New", new { Name = name, Password = password }, token);
                created.EnsureSuccessStatusCode();
                var user = await created.Content.ReadFromJsonAsync<JsonObject>(token);
                createdIds.Add(user!["Id"]!.GetValue<string>());
            }

            async Task Authenticate(HttpClient client, string name, string app, string? targetDeviceId = null)
            {
                var prefix = "MediaBrowser Client=\"" + app + "\", DeviceId=\"" + (targetDeviceId ?? deviceId) + "\", Device=\"Shared fixture\", Version=\"1.0\"";
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, prefix);
                using var login = await client.PostAsJsonAsync("Users/AuthenticateByName", new { Username = name, Pw = password }, token);
                login.EnsureSuccessStatusCode();
                var authentication = await login.Content.ReadFromJsonAsync<JsonObject>(token);
                client.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, prefix + ", Token=\"" + authentication!["AccessToken"]!.GetValue<string>() + "\"");
            }

            using var owner = _factory.CreateClient();
            using var peer = _factory.CreateClient();
            using var otherApp = _factory.CreateClient();
            await Authenticate(owner, names[0], "ScopeAppA");
            await Authenticate(peer, names[1], "ScopeAppA");
            // Existing authentication intentionally replaces a user's prior token on one device.
            // Keep this separate app on another device; the two accounts still share deviceId.
            await Authenticate(otherApp, names[0], "ScopeAppB", deviceId + "-other");
            const string path = "Sessions";
            bool Owned(JsonNode? session) => Guid.TryParse(session?["UserId"]?.GetValue<string>(), out var id)
                && createdIds.Any(value => id.Equals(Guid.Parse(value)));
            var beforeAll = await administrator.GetFromJsonAsync<JsonArray>(path, token);
            var before = beforeAll!.Where(Owned).ToArray();
            Assert.Equal(3, before.Length);
            var removed = Assert.Single(before, session => Guid.Parse(session!["UserId"]!.GetValue<string>()).Equals(Guid.Parse(createdIds[0]))
                && session["Client"]!.GetValue<string>() == "ScopeAppA")!["Id"]!.GetValue<string>();
            var retained = before.Where(session => session!["Id"]!.GetValue<string>() != removed).Select(session => session!["Id"]!.GetValue<string>()).ToArray();
            using var logout = await owner.PostAsync("Sessions/Logout", null, token);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            // Inspect from the administrator before any peer request can recreate an ended session.
            var after = await administrator.GetFromJsonAsync<JsonArray>(path, token);
            var actual = after!.Where(Owned).Select(session => session!["Id"]!.GetValue<string>()).Order().ToArray();
            Assert.Equal(retained.Order().ToArray(), actual);
            using var revoked = await owner.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            using var peerRetained = await peer.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, peerRetained.StatusCode);
            using var appRetained = await otherApp.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.OK, appRetained.StatusCode);
        }
        finally
        {
            foreach (var id in createdIds)
            {
                using var removed = await administrator.DeleteAsync("Users/" + id, token);
                removed.EnsureSuccessStatusCode();
            }
        }
    }
}
