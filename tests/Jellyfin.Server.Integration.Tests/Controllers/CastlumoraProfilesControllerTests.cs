using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraProfilesControllerTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraProfilesControllerTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ManagedKidsProfile_UsesSeparateIdentityPreferencesAndPolicy_AndRevokesPreviousToken()
    {
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation(AuthHelper.AuthHeaderName, AuthHelper.DummyAuthHeader);
        var token = TestContext.Current.CancellationToken;
        using var denied = await anonymous.GetAsync("Castlumora/Profiles", token);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var me = await admin.GetFromJsonAsync<JsonObject>("Users/Me", token);
        var owner = me!["Id"]!.GetValue<string>();
        const string password = "Account-profile-test-41!";
        using var passwordChange = await admin.PostAsJsonAsync("Users/" + owner + "/Password", new { CurrentPw = string.Empty, NewPw = password }, token);
        passwordChange.EnsureSuccessStatusCode();
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "castlumora-profile-library-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var preservedFile = System.IO.Path.Combine(folder, "preserve.txt");
        await System.IO.File.WriteAllTextAsync(preservedFile, "Owned media-library preservation fixture", token);
        try
        {
            using var library = await admin.PostAsJsonAsync("Library/VirtualFolders?name=ProfileFixtures&collectionType=homevideos&paths=" + Uri.EscapeDataString(folder), new { }, token);
            library.EnsureSuccessStatusCode();
            var libraries = await admin.GetFromJsonAsync<JsonArray>("Library/VirtualFolders", token);
            var libraryId = libraries![0]!["ItemId"]!.GetValue<string>();
            using var created = await admin.PostAsJsonAsync("Castlumora/Profiles", new { accountId = owner, name = "Kids", preset = "kids", libraries = new[] { libraryId } }, token);
            Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(token));
            var profile = await created.Content.ReadFromJsonAsync<JsonObject>(token);
            var childId = profile!["id"]!.GetValue<string>();
            var profiles = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Profiles", token);
            Assert.Equal(2, profiles!["profiles"]!.AsArray().Count);
            using var wrong = await admin.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = childId, accountPassword = "wrong" }, token);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            using var switched = await admin.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = childId, accountPassword = password }, token);
            switched.EnsureSuccessStatusCode();
            var authentication = await switched.Content.ReadFromJsonAsync<JsonObject>(token);
            Assert.Equal(Guid.Parse(childId), Guid.Parse(authentication!["User"]!["Id"]!.GetValue<string>()));
            using var child = _factory.CreateClient();
            child.DefaultRequestHeaders.AddAuthHeader(authentication["AccessToken"]!.GetValue<string>());
            using var oldToken = await admin.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);
            var childInfo = await child.GetFromJsonAsync<JsonObject>("Users/Me", token);
            Assert.False(childInfo!["Policy"]!["IsAdministrator"]!.GetValue<bool>());
            Assert.False(childInfo["Policy"]!["EnableAllFolders"]!.GetValue<bool>());
            var renamed = (JsonObject)childInfo.DeepClone();
            renamed["Name"] = "Renamed profile";
            using var rename = await child.PostAsJsonAsync("Users/" + childId, renamed, token);
            Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
            using var adminDenied = await child.PostAsJsonAsync("Castlumora/Profiles", new { }, token);
            Assert.Equal(HttpStatusCode.Forbidden, adminDenied.StatusCode);
            using var guessed = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new { Username = childInfo["Name"]!.GetValue<string>(), Pw = string.Empty }, token);
            Assert.Equal(HttpStatusCode.Unauthorized, guessed.StatusCode);
            var item = Guid.NewGuid().ToString("N");
            using var saved = await child.PutAsync("Castlumora/Presentation/Preferences/hiddenResume/" + item + "?enabled=true", null, token);
            saved.EnsureSuccessStatusCode();
            using var back = await child.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = owner, accountPassword = password }, token);
            back.EnsureSuccessStatusCode();
            var returned = await back.Content.ReadFromJsonAsync<JsonObject>(token);
            using var parent = _factory.CreateClient();
            parent.DefaultRequestHeaders.AddAuthHeader(returned!["AccessToken"]!.GetValue<string>());
            var preferences = await parent.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences", token);
            Assert.Empty(preferences!["hiddenResume"]!.AsArray());
            using var childRevoked = await child.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, childRevoked.StatusCode);

            // Exercise owner disablement through the real administrator policy endpoint.
            using var accountCreation = await parent.PostAsJsonAsync("Users/New", new { Name = "ProfileOwner", Password = password }, token);
            accountCreation.EnsureSuccessStatusCode();
            var account = await accountCreation.Content.ReadFromJsonAsync<JsonObject>(token);
            var accountId = account!["Id"]!.GetValue<string>();
            using var linkedCreation = await parent.PostAsJsonAsync("Castlumora/Profiles", new { accountId, name = "Family", preset = "adult", libraries = new[] { libraryId } }, token);
            linkedCreation.EnsureSuccessStatusCode();
            var linked = await linkedCreation.Content.ReadFromJsonAsync<JsonObject>(token);
            using var login = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new { Username = "ProfileOwner", Pw = password }, token);
            login.EnsureSuccessStatusCode();
            var signedIn = await login.Content.ReadFromJsonAsync<JsonObject>(token);
            using var accountClient = _factory.CreateClient();
            accountClient.DefaultRequestHeaders.AddAuthHeader(signedIn!["AccessToken"]!.GetValue<string>());
            using var profileLogin = await accountClient.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = linked!["id"]!.GetValue<string>(), accountPassword = password }, token);
            profileLogin.EnsureSuccessStatusCode();
            var profileSession = await profileLogin.Content.ReadFromJsonAsync<JsonObject>(token);
            using var linkedClient = _factory.CreateClient();
            linkedClient.DefaultRequestHeaders.AddAuthHeader(profileSession!["AccessToken"]!.GetValue<string>());
            using var enabled = await linkedClient.GetAsync("Users/Me", token);
            enabled.EnsureSuccessStatusCode();
            account["Policy"]!["IsDisabled"] = true;
            using var disable = await parent.PostAsJsonAsync("Users/" + accountId + "/Policy", account["Policy"], token);
            disable.EnsureSuccessStatusCode();
            using var linkedDenied = await linkedClient.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, linkedDenied.StatusCode);
            using var ordinaryStillWorks = await parent.GetAsync("Users/Me", token);
            ordinaryStillWorks.EnsureSuccessStatusCode();
            account["Policy"]!["IsDisabled"] = false;
            using var enable = await parent.PostAsJsonAsync("Users/" + accountId + "/Policy", account["Policy"], token);
            enable.EnsureSuccessStatusCode();
            using var activeAgain = await linkedClient.GetAsync("Users/Me", token);
            activeAgain.EnsureSuccessStatusCode();
            using var listDenied = await linkedClient.GetAsync("Castlumora/Profiles/Managed", token);
            Assert.Equal(HttpStatusCode.Forbidden, listDenied.StatusCode);
            var linkedId = linked!["id"]!.GetValue<string>();
            using var deleteDenied = await linkedClient.DeleteAsync("Castlumora/Profiles/Managed/" + linkedId, token);
            Assert.Equal(HttpStatusCode.Forbidden, deleteDenied.StatusCode);
            using var ownerProtected = await parent.DeleteAsync("Castlumora/Profiles/Managed/" + owner, token);
            Assert.Equal(HttpStatusCode.NotFound, ownerProtected.StatusCode);
            var managed = await parent.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/Managed", token);
            Assert.Equal(2, managed!["profiles"]!.AsArray().Count);
            using var removed = await parent.DeleteAsync("Castlumora/Profiles/Managed/" + linkedId, token);
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            using var deletedSession = await linkedClient.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, deletedSession.StatusCode);
            using var retainedAccount = await parent.GetAsync("Users/" + accountId, token);
            retainedAccount.EnsureSuccessStatusCode();
            Assert.True(System.IO.File.Exists(preservedFile));
            Assert.Single((await parent.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/Managed", token))!["profiles"]!.AsArray());
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }
}
