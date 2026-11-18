using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        using var anonymousPreferences = await anonymous.GetAsync("Castlumora/Profiles/MediaPreferences", token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPreferences.StatusCode);
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
            var paths = _factory.Services.GetRequiredService<IApplicationPaths>();
            var privateRoot = System.IO.Path.Combine(paths.ConfigurationDirectoryPath, "castlumora", "profiles");
            var registryPath = System.IO.Path.Combine(privateRoot, "profiles.json");
            var keyBeforeMigration = await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(privateRoot, "credentials.key"), token);
            var legacy = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(registryPath, token))!;
            legacy["Version"] = 1;
            foreach (var record in legacy["Profiles"]!.AsArray())
            {
                record!.AsObject().Remove("Lifecycle");
                record.AsObject().Remove("PendingLibraries");
            }

            var legacyText = legacy.ToJsonString();
            await System.IO.File.WriteAllTextAsync(registryPath, legacyText, token);
            var migrated = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/Managed", token);
            Assert.Equal("active", migrated!["profiles"]!.AsArray()[0]!["lifecycle"]!.GetValue<string>());
            Assert.Equal(legacyText, await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(privateRoot, "profiles.v1.json"), token));
            Assert.Equal(keyBeforeMigration, await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(privateRoot, "credentials.key"), token));
            var originalChild = await admin.GetFromJsonAsync<JsonObject>("Users/" + childId, token);
            using var invalidRename = await admin.PutAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Name", new { name = "Family", preset = "adult" }, token);
            Assert.Equal(HttpStatusCode.BadRequest, invalidRename.StatusCode);
            using var accountRename = await admin.PutAsJsonAsync("Castlumora/Profiles/Managed/" + owner + "/Name", new { name = "Family" }, token);
            Assert.Equal(HttpStatusCode.NotFound, accountRename.StatusCode);
            using var friendlyRename = await admin.PutAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Name", new { name = " Family viewers " }, token);
            Assert.Equal(HttpStatusCode.NoContent, friendlyRename.StatusCode);
            var renamedChild = await admin.GetFromJsonAsync<JsonObject>("Users/" + childId, token);
            Assert.Equal(originalChild!["Name"]!.GetValue<string>(), renamedChild!["Name"]!.GetValue<string>());
            Assert.Equal(originalChild["Policy"]!.ToJsonString(), renamedChild["Policy"]!.ToJsonString());
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
            using var deniedRename = await child.PutAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Name", new { name = "Unauthorized" }, token);
            Assert.Equal(HttpStatusCode.Forbidden, deniedRename.StatusCode);
            var childProfiles = await child.GetFromJsonAsync<JsonObject>("Castlumora/Profiles", token);
            Assert.Equal("Family viewers", childProfiles!["profiles"]!.AsArray()[1]!["name"]!.GetValue<string>());
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
            using var unsafePreferences = await child.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences", new { audioLanguage = "spa", EnableLocalPassword = true }, token);
            Assert.Equal(HttpStatusCode.BadRequest, unsafePreferences.StatusCode);
            using var targetPreferences = await child.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences", new { userId = owner, audioLanguage = "spa" }, token);
            Assert.Equal(HttpStatusCode.BadRequest, targetPreferences.StatusCode);
            using var invalidLanguage = await child.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences", new { audioLanguage = "../en", subtitleMode = "Always" }, token);
            Assert.Equal(HttpStatusCode.BadRequest, invalidLanguage.StatusCode);
            using var mediaDefaults = await child.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences?userId=" + owner, new { audioLanguage = "spa", subtitleLanguage = "eng", useDefaultAudioTrack = false, subtitleMode = "Always" }, token);
            mediaDefaults.EnsureSuccessStatusCode();
            var savedDefaults = await child.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/MediaPreferences", token);
            Assert.Equal("spa", savedDefaults!["audioLanguage"]!.GetValue<string>());
            Assert.Equal("eng", savedDefaults["subtitleLanguage"]!.GetValue<string>());
            var configuredChild = await child.GetFromJsonAsync<JsonObject>("Users/Me", token);
            Assert.Equal("spa", configuredChild!["Configuration"]!["AudioLanguagePreference"]!.GetValue<string>());
            Assert.Equal(childInfo["Name"]!.GetValue<string>(), configuredChild["Name"]!.GetValue<string>());
            Assert.False(configuredChild["Policy"]!["EnableUserPreferenceAccess"]!.GetValue<bool>());
            Assert.Equal(childInfo["Configuration"]!["EnableLocalPassword"]!.GetValue<bool>(), configuredChild["Configuration"]!["EnableLocalPassword"]!.GetValue<bool>());
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
            var ownerDefaults = await parent.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/MediaPreferences", token);
            Assert.Equal(me["Configuration"]!["AudioLanguagePreference"]?.GetValue<string>(), ownerDefaults!["audioLanguage"]?.GetValue<string>());
            using var childRevoked = await child.GetAsync("Users/Me", token);
            Assert.Equal(HttpStatusCode.Unauthorized, childRevoked.StatusCode);

            // Only test-owned private intent is edited; identity/preferences remain real SQLite data.
            var interrupted = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(registryPath, token))!;
            var pending = interrupted["Profiles"]!.AsArray()[0]!;
            pending["Ready"] = false;
            pending["Lifecycle"] = "provisioning";
            pending["PendingLibraries"] = new JsonArray(JsonValue.Create(Guid.Parse(libraryId)));
            await System.IO.File.WriteAllTextAsync(registryPath, interrupted.ToJsonString(), token);
            var unavailable = await parent.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/Managed", token);
            Assert.True(unavailable!["recoveryAvailable"]!.GetValue<bool>());
            Assert.False(unavailable["profiles"]!.AsArray()[0]!["ready"]!.GetValue<bool>());
            using var anonymousRecovery = await anonymous.PostAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Recover", new { }, token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousRecovery.StatusCode);
            using var overrideRecovery = await parent.PostAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Recover", new { preset = "adult" }, token);
            Assert.Equal(HttpStatusCode.BadRequest, overrideRecovery.StatusCode);
            using var ownerRecovery = await parent.PostAsJsonAsync("Castlumora/Profiles/Managed/" + owner + "/Recover", new { }, token);
            Assert.Equal(HttpStatusCode.NotFound, ownerRecovery.StatusCode);
            using var recovered = await parent.PostAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Recover", new { }, token);
            Assert.Equal(HttpStatusCode.NoContent, recovered.StatusCode);
            using var activeRecovery = await parent.PostAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Recover", new { }, token);
            Assert.Equal(HttpStatusCode.Conflict, activeRecovery.StatusCode);
            Assert.Equal(keyBeforeMigration, await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(privateRoot, "credentials.key"), token));
            var recoveredInfo = await parent.GetFromJsonAsync<JsonObject>("Users/" + childId, token);
            Assert.Equal(configuredChild["Configuration"]!.ToJsonString(), recoveredInfo!["Configuration"]!.ToJsonString());
            Assert.Equal(childInfo["Name"]!.GetValue<string>(), recoveredInfo["Name"]!.GetValue<string>());
            using var recoverSwitch = await parent.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = childId, accountPassword = password }, token);
            recoverSwitch.EnsureSuccessStatusCode();
            var recoveredAuthentication = await recoverSwitch.Content.ReadFromJsonAsync<JsonObject>(token);
            using var recoveredClient = _factory.CreateClient();
            recoveredClient.DefaultRequestHeaders.AddAuthHeader(recoveredAuthentication!["AccessToken"]!.GetValue<string>());
            var recoveredPreferences = await recoveredClient.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences", token);
            Assert.Contains(recoveredPreferences!["hiddenResume"]!.AsArray(), value => value!.GetValue<string>() == item);
            using var childRecovery = await recoveredClient.PostAsJsonAsync("Castlumora/Profiles/Managed/" + childId + "/Recover", new { }, token);
            Assert.Equal(HttpStatusCode.Forbidden, childRecovery.StatusCode);
            using var recoverBack = await recoveredClient.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = owner, accountPassword = password }, token);
            recoverBack.EnsureSuccessStatusCode();
            var restoredParent = await recoverBack.Content.ReadFromJsonAsync<JsonObject>(token);
            parent.DefaultRequestHeaders.Remove(AuthHelper.AuthHeaderName);
            parent.DefaultRequestHeaders.AddAuthHeader(restoredParent!["AccessToken"]!.GetValue<string>());

            // Exercise owner disablement through the real administrator policy endpoint.
            using var accountCreation = await parent.PostAsJsonAsync("Users/New", new { Name = "ProfileOwner", Password = password }, token);
            accountCreation.EnsureSuccessStatusCode();
            var account = await accountCreation.Content.ReadFromJsonAsync<JsonObject>(token);
            var accountId = account!["Id"]!.GetValue<string>();
            account["Policy"]!["EnableUserPreferenceAccess"] = false;
            using var restrictPreferences = await parent.PostAsJsonAsync("Users/" + accountId + "/Policy", account["Policy"], token);
            restrictPreferences.EnsureSuccessStatusCode();
            using var linkedCreation = await parent.PostAsJsonAsync("Castlumora/Profiles", new { accountId, name = "Family", preset = "adult", libraries = new[] { libraryId } }, token);
            linkedCreation.EnsureSuccessStatusCode();
            var linked = await linkedCreation.Content.ReadFromJsonAsync<JsonObject>(token);
            using var login = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new { Username = "ProfileOwner", Pw = password }, token);
            login.EnsureSuccessStatusCode();
            var signedIn = await login.Content.ReadFromJsonAsync<JsonObject>(token);
            using var accountClient = _factory.CreateClient();
            accountClient.DefaultRequestHeaders.AddAuthHeader(signedIn!["AccessToken"]!.GetValue<string>());
            var restrictedDefaults = await accountClient.GetFromJsonAsync<JsonObject>("Castlumora/Profiles/MediaPreferences", token);
            Assert.False(restrictedDefaults!["canEdit"]!.GetValue<bool>());
            using var restrictedWrite = await accountClient.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences", new { audioLanguage = "spa" }, token);
            Assert.Equal(HttpStatusCode.Forbidden, restrictedWrite.StatusCode);
            using var profileLogin = await accountClient.PostAsJsonAsync("Castlumora/Profiles/Switch", new { profileId = linked!["id"]!.GetValue<string>(), accountPassword = password }, token);
            profileLogin.EnsureSuccessStatusCode();
            var profileSession = await profileLogin.Content.ReadFromJsonAsync<JsonObject>(token);
            using var linkedClient = _factory.CreateClient();
            linkedClient.DefaultRequestHeaders.AddAuthHeader(profileSession!["AccessToken"]!.GetValue<string>());
            using var enabled = await linkedClient.GetAsync("Users/Me", token);
            enabled.EnsureSuccessStatusCode();
            using var childPreferenceDenied = await linkedClient.PutAsJsonAsync("Castlumora/Profiles/MediaPreferences", new { audioLanguage = "spa" }, token);
            Assert.Equal(HttpStatusCode.Forbidden, childPreferenceDenied.StatusCode);
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
