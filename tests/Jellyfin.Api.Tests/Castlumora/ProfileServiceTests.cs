using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Users;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ProfileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-profiles-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IUserManager> _users = new();
    private readonly Mock<ILibraryManager> _libraries = new();
    private readonly Mock<ISessionManager> _sessions = new();
    private readonly Dictionary<Guid, User> _people = [];
    private readonly Dictionary<Guid, UserPolicy> _policies = [];
    private readonly Dictionary<Guid, string> _passwords = [];
    private readonly Guid _folder = Guid.NewGuid();
    private readonly User _owner = new("Account", "auth", "reset") { Password = "existing hash" };
    private readonly List<string> _events = [];
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _owner.SetPermission(PermissionKind.IsAdministrator, true);
        _people.Add(_owner.Id, _owner);
        _policies.Add(_owner.Id, new UserPolicy { IsAdministrator = true });
        _users.Setup(users => users.GetUserById(It.IsAny<Guid>())).Returns((Guid id) => _people.GetValueOrDefault(id));
        _users.Setup(users => users.GetUserDto(It.IsAny<User>(), It.IsAny<string?>())).Returns((User user, string? _) => new UserDto { Id = user.Id, Name = user.Username, Policy = _policies[user.Id] });
        _users.Setup(users => users.CreateUserAsync(It.IsAny<string>(), true)).Returns((string name, bool _) =>
        {
            var user = new User(name, "auth", "reset");
            user.SetPermission(PermissionKind.IsDisabled, true);
            _people.Add(user.Id, user);
            _policies.Add(user.Id, new UserPolicy { IsDisabled = true });
            _events.Add("disabled");
            return Task.FromResult(user);
        });
        _users.Setup(users => users.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).Returns((Guid id, string password) =>
        {
            Assert.True(_people[id].HasPermission(PermissionKind.IsDisabled));
            _passwords[id] = password;
            _events.Add("password");
            return Task.CompletedTask;
        });
        _users.Setup(users => users.UpdatePolicyAsync(It.IsAny<Guid>(), It.IsAny<UserPolicy>())).Returns((Guid id, UserPolicy policy) =>
        {
            _policies[id] = policy;
            _people[id].SetPermission(PermissionKind.IsDisabled, policy.IsDisabled);
            _events.Add("policy");
            return Task.CompletedTask;
        });
        _users.Setup(users => users.AuthenticateUser(_owner.Username, "account password", It.IsAny<string>(), true)).ReturnsAsync(_owner);
        _libraries.Setup(libraries => libraries.GetVirtualFolders()).Returns([new VirtualFolderInfo { ItemId = _folder.ToString("N"), Name = "Allowed" }]);
        _sessions.Setup(sessions => sessions.AuthenticateNewSession(It.IsAny<AuthenticationRequest>())).Returns((AuthenticationRequest request) =>
        {
            var user = _people.Values.Single(user => user.Username == request.Username);
            Assert.Equal(_passwords[user.Id], request.Password);
            _events.Add("authenticate");
            return Task.FromResult(new AuthenticationResult { User = new UserDto { Id = user.Id }, AccessToken = "child-token" });
        });
        _sessions.Setup(sessions => sessions.Logout(It.IsAny<string>())).Callback((string _) => _events.Add("revoke")).Returns(Task.CompletedTask);
        _sessions.Setup(sessions => sessions.RevokeUserTokens(It.IsAny<Guid>(), It.IsAny<string>())).Callback(() => _events.Add("revoke-all")).Returns(Task.CompletedTask);
        _users.Setup(users => users.DeleteUserAsync(It.IsAny<Guid>())).Returns((Guid id) =>
        {
            Assert.True(_people[id].HasPermission(PermissionKind.IsDisabled));
            _events.Add("delete");
            _people.Remove(id);
            return Task.CompletedTask;
        });
        _service = Open();
    }

    [Fact]
    public async Task Recover_ResumesPersistedIntentWithSameIdentityKeyAndCurrentOwnerBounds()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await InterruptProvisioning();
        var user = _people[child];
        user.AudioLanguagePreference = "spa";
        var keyPath = Path.Combine(_root, "castlumora", "profiles", "credentials.key");
        var key = await File.ReadAllBytesAsync(keyPath, token);
        _policies[_owner.Id].EnableRemoteAccess = false;
        _policies[_owner.Id].MaxParentalRating = 3;
        _events.Clear();
        using var reopened = Open();
        Assert.False(await reopened.SessionAllowedAsync(child, token));
        Assert.True(await reopened.RecoverAsync(_owner.Id, child, token));
        Assert.Equal(new[] { "policy", "revoke-all", "password", "policy" }, _events);
        Assert.Same(user, _people[child]);
        Assert.Equal("spa", user.AudioLanguagePreference);
        Assert.Equal(3, _policies[child].MaxParentalRating);
        Assert.False(_policies[child].EnableRemoteAccess);
        Assert.Equal("auth", _policies[child].AuthenticationProviderId);
        Assert.False(_policies[child].IsAdministrator);
        Assert.Equal(new[] { _folder }, _policies[child].EnabledFolders);
        Assert.Equal(key, await File.ReadAllBytesAsync(keyPath, token));
        using var afterRestart = Open();
        Assert.True(await afterRestart.SessionAllowedAsync(child, token));
        var record = (await afterRestart.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray()[0]!;
        Assert.True(record["ready"]!.GetValue<bool>());
        Assert.Equal("active", record["lifecycle"]!.GetValue<string>());
        _users.Verify(users => users.CreateUserAsync(It.IsAny<string>(), true), Times.Once);
        _sessions.Verify(sessions => sessions.RevokeUserTokens(child, null), Times.Once);
        _sessions.Verify(sessions => sessions.RevokeUserTokens(_owner.Id, It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("deleting", false)]
    [InlineData("review", false)]
    public async Task Recover_NeverReactivatesOtherLifecycles(string lifecycle, bool ready)
    {
        var child = await InterruptProvisioning();
        var path = Path.Combine(_root, "castlumora", "profiles", "profiles.json");
        var state = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!;
        var profile = state["Profiles"]!.AsArray()[0]!;
        profile["Lifecycle"] = lifecycle;
        profile["Ready"] = ready;
        profile["PendingLibraries"] = null;
        await File.WriteAllTextAsync(path, state.ToJsonString(), TestContext.Current.CancellationToken);
        var original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        _events.Clear();
        using var reopened = Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.RecoverAsync(_owner.Id, child, TestContext.Current.CancellationToken));
        Assert.Empty(_events);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("disabled-owner")]
    [InlineData("passwordless-owner")]
    [InlineData("missing-owner")]
    [InlineData("missing-child")]
    [InlineData("administrator-child")]
    [InlineData("ordinary-identity")]
    [InlineData("missing-library")]
    [InlineData("denied-library")]
    [InlineData("missing-key")]
    public async Task Recover_RejectsUnsafePrerequisitesBeforeMutations(string problem)
    {
        var child = await InterruptProvisioning();
        var keyPath = Path.Combine(_root, "castlumora", "profiles", "credentials.key");
        // A separate real administrator can inspect an unavailable owner's profile.
        var administrator = new User("Administrator", "auth", "reset");
        administrator.SetPermission(PermissionKind.IsAdministrator, true);
        _people.Add(administrator.Id, administrator);
        switch (problem)
        {
            case "disabled-owner":
                _owner.SetPermission(PermissionKind.IsDisabled, true);
                break;
            case "passwordless-owner":
                _owner.Password = null;
                break;
            case "missing-owner":
                _people.Remove(_owner.Id);
                break;
            case "missing-child":
                _people.Remove(child);
                break;
            case "administrator-child":
                _people[child].SetPermission(PermissionKind.IsAdministrator, true);
                break;
            case "ordinary-identity":
                _people[child].Username = "Ordinary account";
                break;
            case "missing-library":
                _libraries.Setup(libraries => libraries.GetVirtualFolders()).Returns([]);
                break;
            case "denied-library":
                _policies[_owner.Id].EnableAllFolders = false;
                _policies[_owner.Id].EnabledFolders = [];
                break;
            case "missing-key":
                File.Delete(keyPath);
                break;
        }

        _events.Clear();
        using var reopened = Open();
        var exception = await Record.ExceptionAsync(() => reopened.RecoverAsync(administrator.Id, child, TestContext.Current.CancellationToken));
        if (problem == "missing-key")
        {
            Assert.IsType<InvalidDataException>(exception);
            Assert.False(File.Exists(keyPath));
        }
        else
        {
            Assert.IsType<InvalidOperationException>(exception);
        }

        Assert.Empty(_events);
        Assert.False(await reopened.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Recover_FailureAfterPolicyEnableRemainsDeniedAcrossRestartAndRetryPreservesIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await InterruptProvisioning();
        _users.Setup(users => users.UpdatePolicyAsync(child, It.IsAny<UserPolicy>())).Returns((Guid id, UserPolicy policy) =>
        {
            _policies[id] = policy;
            _people[id].SetPermission(PermissionKind.IsDisabled, policy.IsDisabled);
            return policy.IsDisabled ? Task.CompletedTask : Task.FromException(new IOException("Interrupted after core policy update"));
        });
        using var reopened = Open();
        await Assert.ThrowsAsync<IOException>(() => reopened.RecoverAsync(_owner.Id, child, token));
        Assert.False(_people[child].HasPermission(PermissionKind.IsDisabled));
        using var afterFailure = Open();
        Assert.False(await afterFailure.SessionAllowedAsync(child, token));
        Assert.Equal("provisioning", (await afterFailure.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray()[0]!["lifecycle"]!.GetValue<string>());
        _users.Setup(users => users.UpdatePolicyAsync(child, It.IsAny<UserPolicy>())).Returns((Guid id, UserPolicy policy) =>
        {
            _policies[id] = policy;
            _people[id].SetPermission(PermissionKind.IsDisabled, policy.IsDisabled);
            return Task.CompletedTask;
        });
        Assert.True(await afterFailure.RecoverAsync(_owner.Id, child, token));
        Assert.True(await afterFailure.SessionAllowedAsync(child, token));
        _users.Verify(users => users.CreateUserAsync(It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public async Task Recover_RequiresAdministratorAndRejectsCancellationBeforeRepair()
    {
        var child = await InterruptProvisioning();
        _events.Clear();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RecoverAsync(child, child, TestContext.Current.CancellationToken));
        Assert.False(await _service.RecoverAsync(_owner.Id, Guid.NewGuid(), TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.RecoverAsync(_owner.Id, child, cancellation.Token));
        Assert.Empty(_events);
    }

    private async Task<Guid> InterruptProvisioning()
    {
        _users.Setup(users => users.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(new IOException("Interrupted provisioning"));
        await Assert.ThrowsAsync<IOException>(() => Create());
        var child = Assert.Single(_people.Keys, id => !id.Equals(_owner.Id));
        _users.Setup(users => users.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).Returns((Guid id, string password) =>
        {
            Assert.True(_people[id].HasPermission(PermissionKind.IsDisabled));
            _passwords[id] = password;
            _events.Add("password");
            return Task.CompletedTask;
        });
        return child;
    }

    [Fact]
    public async Task Lifecycle_InterruptedProvisioningAndDeletionRemainDistinctAcrossReopen()
    {
        var token = TestContext.Current.CancellationToken;
        _users.Setup(users => users.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(new IOException("Interrupted provisioning"));
        await Assert.ThrowsAsync<IOException>(() => Create());
        var child = Assert.Single(_people.Keys, id => !id.Equals(_owner.Id));
        var root = Path.Combine(_root, "castlumora", "profiles");
        var registry = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "profiles.json"), token))!;
        var pending = registry["Profiles"]!.AsArray()[0]!;
        Assert.Equal(2, registry["Version"]!.GetValue<int>());
        Assert.Equal("provisioning", pending["Lifecycle"]!.GetValue<string>());
        Assert.Equal(_folder, Guid.Parse(pending["PendingLibraries"]!.AsArray()[0]!.GetValue<string>()));
        using var reopened = Open();
        Assert.Equal("provisioning", (await reopened.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray()[0]!["lifecycle"]!.GetValue<string>());
        Assert.False(await reopened.SessionAllowedAsync(child, token));
        _users.Setup(users => users.DeleteUserAsync(child)).ThrowsAsync(new IOException("Interrupted deletion"));
        await Assert.ThrowsAsync<IOException>(() => reopened.DeleteAsync(_owner.Id, child, token));
        using var afterDelete = Open();
        var removal = (await afterDelete.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray()[0]!;
        Assert.Equal("deleting", removal["lifecycle"]!.GetValue<string>());
        Assert.False(removal["ready"]!.GetValue<bool>());
        Assert.False(await afterDelete.SessionAllowedAsync(child, token));
        Assert.True(_people[child].HasPermission(PermissionKind.IsDisabled));
        registry = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "profiles.json"), token))!;
        Assert.Null(registry["Profiles"]!.AsArray()[0]!["PendingLibraries"]);
    }

    [Fact]
    public async Task Lifecycle_LegacyMigrationPreservesSnapshotKeyPoliciesAndDeniesAmbiguousPending()
    {
        var token = TestContext.Current.CancellationToken;
        var active = await Create();
        var second = Guid.Parse((await _service.CreateAsync(_owner.Id, _owner.Id, "Adult", "adult", [_folder], token))["id"]!.GetValue<string>());
        var root = Path.Combine(_root, "castlumora", "profiles");
        var path = Path.Combine(root, "profiles.json");
        var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path, token))!;
        legacy["Version"] = 1;
        foreach (var profile in legacy["Profiles"]!.AsArray())
        {
            profile!.AsObject().Remove("Lifecycle");
            profile.AsObject().Remove("PendingLibraries");
        }

        legacy["Profiles"]!.AsArray()[1]!["Ready"] = false;
        var original = Encoding.UTF8.GetBytes(legacy.ToJsonString());
        await File.WriteAllBytesAsync(path, original, token);
        var key = await File.ReadAllBytesAsync(Path.Combine(root, "credentials.key"), token);
        var activePolicy = _policies[active];
        _events.Clear();
        using var reopened = Open();
        var profiles = (await reopened.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray();
        Assert.Equal("active", profiles[0]!["lifecycle"]!.GetValue<string>());
        Assert.Equal("review", profiles[1]!["lifecycle"]!.GetValue<string>());
        Assert.False(profiles[1]!["ready"]!.GetValue<bool>());
        Assert.True(await reopened.SessionAllowedAsync(active, token));
        Assert.False(await reopened.SessionAllowedAsync(second, token));
        Assert.False(_people[second].HasPermission(PermissionKind.IsDisabled));
        Assert.Same(activePolicy, _policies[active]);
        Assert.Empty(_events);
        Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(root, "profiles.v1.json"), token));
        Assert.Equal(key, await File.ReadAllBytesAsync(Path.Combine(root, "credentials.key"), token));
        var upgraded = JsonNode.Parse(await File.ReadAllTextAsync(path, token))!;
        Assert.Equal(2, upgraded["Version"]!.GetValue<int>());
        // Model a crash after the private snapshot was committed but before the registry replacement.
        await File.WriteAllBytesAsync(path, original, token);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.Combine(root, "profiles.v1.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }

        using var again = Open();
        Assert.Equal(2, (await again.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray().Count);
        Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(root, "profiles.v1.json"), token));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(root, "profiles.v1.json")));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public async Task Lifecycle_ConflictingOrLinkedLegacySnapshotFailsClosedWithoutOverwriting()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var root = Path.Combine(_root, "castlumora", "profiles");
        var path = Path.Combine(root, "profiles.json");
        var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path, token))!;
        legacy["Version"] = 1;
        var original = legacy.ToJsonString();
        await File.WriteAllTextAsync(path, original, token);
        var snapshot = Path.Combine(root, "profiles.v1.json");
        await File.WriteAllTextAsync(snapshot, "Preserve earlier snapshot", token);
        using var reopened = Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.ListManagedAsync(_owner.Id, token));
        Assert.False(await reopened.SessionAllowedAsync(child, token));
        Assert.True(await reopened.SessionAllowedAsync(_owner.Id, token));
        Assert.Equal(original, await File.ReadAllTextAsync(path, token));
        Assert.Equal("Preserve earlier snapshot", await File.ReadAllTextAsync(snapshot, token));
        if (!OperatingSystem.IsWindows())
        {
            File.Delete(snapshot);
            File.CreateSymbolicLink(snapshot, path);
            await Assert.ThrowsAsync<InvalidDataException>(() => reopened.ListManagedAsync(_owner.Id, token));
            Assert.Equal(original, await File.ReadAllTextAsync(path, token));
        }
    }

    [Theory]
    [InlineData("active", false, "none")]
    [InlineData("deleting", true, "none")]
    [InlineData("unknown", false, "none")]
    [InlineData("provisioning", false, "none")]
    [InlineData("provisioning", false, "empty")]
    [InlineData("provisioning", false, "duplicate")]
    [InlineData("active", true, "valid")]
    public async Task Lifecycle_MalformedStateCannotGrantManagedAccess(string lifecycle, bool ready, string plan)
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var path = Path.Combine(_root, "castlumora", "profiles", "profiles.json");
        var state = JsonNode.Parse(await File.ReadAllTextAsync(path, token))!;
        var profile = state["Profiles"]!.AsArray()[0]!;
        profile["Lifecycle"] = lifecycle;
        profile["Ready"] = ready;
        profile["PendingLibraries"] = plan switch
        {
            "valid" => new JsonArray(_folder.ToString()),
            "duplicate" => new JsonArray(_folder.ToString(), _folder.ToString()),
            "empty" => new JsonArray(Guid.Empty.ToString()),
            _ => null
        };
        var original = state.ToJsonString();
        await File.WriteAllTextAsync(path, original, token);
        using var reopened = Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => reopened.ListManagedAsync(_owner.Id, token));
        Assert.False(await reopened.SessionAllowedAsync(child, token));
        Assert.True(await reopened.SessionAllowedAsync(_owner.Id, token));
        Assert.Equal(original, await File.ReadAllTextAsync(path, token));
    }

    [Fact]
    public async Task Lifecycle_LibraryPlanAndAppliedPolicyUseAnIsolatedInputSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        var requested = new[] { _folder };
        var foreign = Guid.NewGuid();
        _policies[_owner.Id].EnableAllFolders = false;
        _policies[_owner.Id].EnabledFolders = [_folder];
        _users.Setup(users => users.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).Returns((Guid id, string password) =>
        {
            Assert.True(_people[id].HasPermission(PermissionKind.IsDisabled));
            _passwords[id] = password;
            requested[0] = foreign;
            return Task.CompletedTask;
        });
        var child = Guid.Parse((await _service.CreateAsync(_owner.Id, _owner.Id, "Snapshot", "kids", requested, token))["id"]!.GetValue<string>());
        Assert.Equal(foreign, requested[0]);
        Assert.Equal(new[] { _folder }, _policies[child].EnabledFolders);
        Assert.True(await _service.SessionAllowedAsync(child, token));
    }

    [Fact]
    public async Task Lifecycle_RegistryBudgetRejectsProvisionBeforeCreatingAnOrphan()
    {
        var token = TestContext.Current.CancellationToken;
        var folders = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        _libraries.Setup(libraries => libraries.GetVirtualFolders()).Returns(folders.Select(id => new VirtualFolderInfo { ItemId = id.ToString() }).ToList());
        JsonObject Entry() => new()
        {
            ["Owner"] = Guid.NewGuid().ToString(),
            ["User"] = Guid.NewGuid().ToString(),
            ["Name"] = new string('x', 40),
            ["Preset"] = "kids",
            ["Ready"] = false,
            ["Lifecycle"] = "provisioning",
            ["PendingLibraries"] = new JsonArray(folders.Select(id => JsonValue.Create(id.ToString())).ToArray<JsonNode?>())
        };
        var sample = Entry();
        var count = ((2097152 - 262144) - 100) / (Encoding.UTF8.GetByteCount(sample.ToJsonString()) + 1);
        var profiles = new JsonArray(Enumerable.Range(0, count).Select(_ => (JsonNode)Entry()).ToArray());
        var state = new JsonObject { ["Version"] = 2, ["Profiles"] = profiles };
        var original = Encoding.UTF8.GetBytes(state.ToJsonString());
        var root = Path.Combine(_root, "castlumora", "profiles");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "profiles.json");
        await File.WriteAllBytesAsync(path, original, token);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _service.CreateAsync(_owner.Id, _owner.Id, "New", "kids", folders, token));
        Assert.Contains("registry", error.Message, StringComparison.OrdinalIgnoreCase);
        _users.Verify(users => users.CreateUserAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        Assert.False(File.Exists(Path.Combine(root, "credentials.key")));
        Assert.Equal(original, await File.ReadAllBytesAsync(path, token));
    }

    [Fact]
    public async Task Rename_PreservesCoreIdentityCredentialsPolicyAndAuthentication_AcrossReopen()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var username = _people[child].Username;
        var password = _passwords[child];
        var policy = _policies[child];
        var keyPath = Path.Combine(_root, "castlumora", "profiles", "credentials.key");
        var key = await File.ReadAllBytesAsync(keyPath, token);
        _events.Clear();
        Assert.True(await _service.RenameAsync(_owner.Id, child, "  Family time  ", token));
        Assert.Empty(_events);
        Assert.Equal(username, _people[child].Username);
        Assert.Equal(password, _passwords[child]);
        Assert.Same(policy, _policies[child]);
        Assert.Equal(key, await File.ReadAllBytesAsync(keyPath, token));
        using var reopened = Open();
        var state = await reopened.ListAsync(child, token);
        Assert.Equal("Family time", state["profiles"]!.AsArray()[1]!["name"]!.GetValue<string>());
        Assert.True(await reopened.SessionAllowedAsync(child, token));
        var authenticated = await reopened.SwitchAsync(_owner.Id, child, "account password", Auth(), "127.0.0.1", token);
        Assert.Equal(child, authenticated.User.Id);
    }

    [Fact]
    public async Task Rename_RejectsNonAdministratorsInvalidNamesDuplicatesAndOwningAccounts()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RenameAsync(child, child, "Other", token));
        Assert.False(await _service.RenameAsync(_owner.Id, _owner.Id, "Other", token));
        Assert.False(await _service.RenameAsync(_owner.Id, Guid.NewGuid(), "Other", token));
        foreach (var name in new[] { string.Empty, "  ", "Bad\nName", new string('x', 41) })
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => _service.RenameAsync(_owner.Id, child, name, token));
        }

        await _service.CreateAsync(_owner.Id, _owner.Id, "Second", "adult", [_folder], token);
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.RenameAsync(_owner.Id, child, " second ", token));
        Assert.True(await _service.RenameAsync(_owner.Id, child, "KIDS", token));
        Assert.Equal("KIDS", (await _service.ListAsync(child, token))["profiles"]!.AsArray()[1]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Rename_DoesNotReactivateAnUnavailableProfile()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        _users.Setup(users => users.DeleteUserAsync(child)).ThrowsAsync(new IOException("Simulated interrupted deletion"));
        await Assert.ThrowsAsync<IOException>(() => _service.DeleteAsync(_owner.Id, child, token));
        Assert.True(await _service.RenameAsync(_owner.Id, child, "Pending review", token));
        Assert.False(await _service.SessionAllowedAsync(child, token));
        var inventory = await _service.ListManagedAsync(_owner.Id, token);
        Assert.False(inventory["profiles"]!.AsArray()[0]!["ready"]!.GetValue<bool>());
        Assert.True(_people[child].HasPermission(PermissionKind.IsDisabled));
    }

    [Fact]
    public async Task Delete_RequiresAdministrator_RevokesOnlyTheChild_AndPreservesOwnerAndKey()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var keyPath = Path.Combine(_root, "castlumora", "profiles", "credentials.key");
        var key = await File.ReadAllBytesAsync(keyPath, token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ListManagedAsync(child, token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteAsync(child, child, token));
        Assert.False(await _service.DeleteAsync(_owner.Id, _owner.Id, token));
        var listed = await _service.ListManagedAsync(_owner.Id, token);
        Assert.Single(listed["profiles"]!.AsArray());
        _events.Clear();
        Assert.True(await _service.DeleteAsync(_owner.Id, child, token));
        Assert.Equal(new[] { "policy", "revoke-all", "delete" }, _events);
        Assert.True(_people.ContainsKey(_owner.Id));
        Assert.False(_people.ContainsKey(child));
        Assert.Equal(key, await File.ReadAllBytesAsync(keyPath, token));
        using var reopened = Open();
        Assert.Empty((await reopened.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray());
        _sessions.Verify(sessions => sessions.RevokeUserTokens(child, null), Times.Once);
        _sessions.Verify(sessions => sessions.RevokeUserTokens(_owner.Id, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Delete_FailureLeavesUnavailableReservation_AndRetryOrOrphanCleanupCompletes()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var attempts = 0;
        _users.Setup(users => users.DeleteUserAsync(child)).Returns(() =>
        {
            if (attempts++ == 0)
            {
                throw new IOException("Owned deletion failure fixture");
            }

            _people.Remove(child);
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<IOException>(() => _service.DeleteAsync(_owner.Id, child, token));
        Assert.True(_people[child].HasPermission(PermissionKind.IsDisabled));
        Assert.False(await _service.SessionAllowedAsync(child, token));
        using var reopened = Open();
        var pending = Assert.Single((await reopened.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray());
        Assert.False(pending!["ready"]!.GetValue<bool>());
        Assert.True(await reopened.DeleteAsync(_owner.Id, child, token));
        var orphan = await Create();
        _people.Remove(orphan);
        Assert.True(await _service.DeleteAsync(_owner.Id, orphan, token));
        Assert.Empty((await reopened.ListManagedAsync(_owner.Id, token))["profiles"]!.AsArray());
        Assert.False(await reopened.DeleteAsync(_owner.Id, orphan, token));
    }

    [Fact]
    public async Task SessionPolicy_DisablesLinkedProfilesWhenOwnerDisabled_AndPreservesOrdinaryAccountsOnDamage()
    {
        var child = await Create();
        Assert.True(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        _owner.SetPermission(PermissionKind.IsDisabled, true);
        Assert.False(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        _owner.SetPermission(PermissionKind.IsDisabled, false);
        _people[child].SetPermission(PermissionKind.IsAdministrator, true);
        Assert.False(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        _people[child].SetPermission(PermissionKind.IsAdministrator, false);
        await File.WriteAllTextAsync(Path.Combine(_root, "castlumora", "profiles", "profiles.json"), "broken", TestContext.Current.CancellationToken);
        Assert.False(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        Assert.True(await _service.SessionAllowedAsync(_owner.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SessionPolicy_FailsClosedAfterRestartWithoutRegistry_AndRejectsTightenedOwnerPolicy()
    {
        var child = await Create();
        _policies[_owner.Id].EnableRemoteAccess = false;
        Assert.False(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        _policies[_owner.Id].EnableRemoteAccess = true;
        Assert.True(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        File.Delete(Path.Combine(_root, "castlumora", "profiles", "profiles.json"));
        using var restarted = Open();
        Assert.False(await restarted.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
        Assert.True(await restarted.SessionAllowedAsync(_owner.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_ClampsPermissionsToOwner_AndKeepsIdentityChangesAdministratorControlled()
    {
        var parent = _policies[_owner.Id];
        parent.EnableRemoteAccess = false;
        parent.EnableContentDownloading = false;
        parent.EnableVideoPlaybackTranscoding = false;
        parent.MaxParentalRating = 3;
        parent.RemoteClientBitrateLimit = 1000000;
        parent.MaxActiveSessions = 2;
        parent.BlockedTags = ["Restricted"];
        var child = await Create();
        var policy = _policies[child];
        Assert.False(policy.EnableRemoteAccess);
        Assert.False(policy.EnableVideoPlaybackTranscoding);
        Assert.False(policy.EnableContentDownloading);
        Assert.False(policy.EnableUserPreferenceAccess);
        Assert.Equal(3, policy.MaxParentalRating);
        Assert.Equal(1000000, policy.RemoteClientBitrateLimit);
        Assert.Equal(2, policy.MaxActiveSessions);
        Assert.Equal(parent.BlockedTags, policy.BlockedTags);
        Assert.True(await _service.SessionAllowedAsync(child, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAndRestart_PersistOwnershipAndPrivateCredentials_WithStrictKidsPolicy()
    {
        var child = await Create();
        Assert.Equal(new[] { "disabled", "password", "policy" }, _events);
        var policy = _policies[child];
        Assert.False(policy.IsAdministrator);
        Assert.False(policy.EnableAllFolders);
        Assert.Equal(new[] { _folder }, policy.EnabledFolders);
        Assert.False(policy.EnableContentDownloading);
        Assert.False(policy.EnableLiveTvManagement);
        Assert.Equal("auth", policy.AuthenticationProviderId);
        Assert.Equal("reset", policy.PasswordResetProviderId);
        Assert.NotEmpty(policy.BlockUnratedItems);
        Assert.True(policy.IsHidden);
        using var reopened = Open();
        var state = await reopened.ListAsync(_owner.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, state["profiles"]!.AsArray().Count);
        var registry = await File.ReadAllTextAsync(Path.Combine(_root, "castlumora", "profiles", "profiles.json"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(_passwords[child], registry, StringComparison.Ordinal);
        Assert.DoesNotContain("account password", registry, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_root, "castlumora", "profiles", "credentials.key")));
        }
    }

    [Fact]
    public async Task Switch_ReauthenticatesAndUsesInheritedChildAuthentication_ThenRevokesOldToken()
    {
        var child = await Create();
        _events.Clear();
        var result = await _service.SwitchAsync(_owner.Id, child, "account password", Auth(), "127.0.0.1", TestContext.Current.CancellationToken);
        Assert.Equal(child, result.User.Id);
        Assert.Equal(new[] { "authenticate", "revoke" }, _events);
        _users.Verify(users => users.AuthenticateUser(_owner.Username, "account password", "127.0.0.1", true), Times.Once);
        _sessions.Verify(sessions => sessions.AuthenticateDirect(It.IsAny<AuthenticationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Switch_RejectsWrongPasswordForeignProfileAdministratorEscalationAndMissingKey()
    {
        var child = await Create();
        await Assert.ThrowsAsync<MediaBrowser.Controller.Authentication.AuthenticationException>(() => _service.SwitchAsync(_owner.Id, child, "wrong", Auth(), "127.0.0.1", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SwitchAsync(Guid.NewGuid(), child, "account password", Auth(), "127.0.0.1", TestContext.Current.CancellationToken));
        _people[child].SetPermission(PermissionKind.IsAdministrator, true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SwitchAsync(_owner.Id, child, "account password", Auth(), "127.0.0.1", TestContext.Current.CancellationToken));
        _people[child].SetPermission(PermissionKind.IsAdministrator, false);
        File.Delete(Path.Combine(_root, "castlumora", "profiles", "credentials.key"));
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.SwitchAsync(_owner.Id, child, "account password", Auth(), "127.0.0.1", TestContext.Current.CancellationToken));
        _sessions.Verify(sessions => sessions.AuthenticateNewSession(It.IsAny<AuthenticationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Create_RejectsViewerInvalidLibrariesAndNestedOwnership()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(Guid.NewGuid(), _owner.Id, "Kids", "kids", [_folder], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.CreateAsync(_owner.Id, _owner.Id, "Kids", "kids", [Guid.NewGuid()], TestContext.Current.CancellationToken));
        var child = await Create();
        _people[child].Password = "hash";
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.CreateAsync(_owner.Id, child, "Nested", "adult", [_folder], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OwnerSessions_RejectsChildForeignAndAccountTargetsWithoutCallingOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var calls = 0;
        Task<int> Operation(string name)
        {
            calls++;
            return Task.FromResult(1);
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UseOwnedProfileSessionsAsync(child, child, Operation, token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UseOwnedProfileSessionsAsync(_owner.Id, _owner.Id, Operation, token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UseOwnedProfileSessionsAsync(_owner.Id, Guid.NewGuid(), Operation, token));
        Assert.Equal(0, calls);
        _owner.SetPermission(PermissionKind.IsDisabled, true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UseOwnedProfileSessionsAsync(_owner.Id, child, Operation, token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OwnerSessions_AllowsRevocationOfUnavailableProfileWithoutReactivationOrWrites()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await InterruptProvisioning();
        var registryPath = Path.Combine(_root, "castlumora", "profiles", "profiles.json");
        var before = await File.ReadAllBytesAsync(registryPath, token);
        Assert.Equal("Kids", await _service.UseOwnedProfileSessionsAsync(_owner.Id, child, Task.FromResult, token));
        Assert.Equal(before, await File.ReadAllBytesAsync(registryPath, token));
        Assert.False(await _service.SessionAllowedAsync(child, token));
    }

    [Fact]
    public async Task OwnerSessions_CorruptRegistryCannotAuthorizeAndFailureReleasesGate()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UseOwnedProfileSessionsAsync<int>(_owner.Id, child, _ => throw new InvalidOperationException(), token));
        Assert.Equal(1, await _service.UseOwnedProfileSessionsAsync(_owner.Id, child, _ => Task.FromResult(1), token));
        var registryPath = Path.Combine(_root, "castlumora", "profiles", "profiles.json");
        await File.WriteAllTextAsync(registryPath, "damaged registry", token);
        var called = false;
        Task<int> RecordOperation(string name)
        {
            called = true;
            return Task.FromResult(1);
        }

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _service.UseOwnedProfileSessionsAsync(_owner.Id, child, RecordOperation, token));
        Assert.False(called);
    }

    [Fact]
    public async Task OwnerSessions_SerializesDeletionUntilSessionOperationCompletes()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<int> GateOperation(string name)
        {
            entered.SetResult();
            await release.Task;
            return 1;
        }

        var operation = _service.UseOwnedProfileSessionsAsync(_owner.Id, child, GateOperation, token);
        await entered.Task;
        var deletion = _service.DeleteAsync(_owner.Id, child, token);
        try
        {
            Assert.False(deletion.IsCompleted);
        }
        finally
        {
            release.SetResult();
        }

        Assert.Equal(1, await operation);
        Assert.True(await deletion);
    }

    [Fact]
    public async Task AccountSessions_DerivesChildrenAndPreservesRegistryAndCredentials()
    {
        var token = TestContext.Current.CancellationToken;
        var child = await Create();
        var privateRoot = Path.Combine(_root, "castlumora", "profiles");
        var registry = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), token);
        var key = await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), token);
        var identities = await _service.UseOwnedAccountSessionsAsync(_owner.Id, Task.FromResult, token);
        Assert.Equal(new[] { child }, identities);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UseOwnedAccountSessionsAsync(child, Task.FromResult, token));
        Assert.Equal(registry, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "profiles.json"), token));
        Assert.Equal(key, await File.ReadAllBytesAsync(Path.Combine(privateRoot, "credentials.key"), token));
        _owner.SetPermission(PermissionKind.IsDisabled, true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UseOwnedAccountSessionsAsync(_owner.Id, Task.FromResult, token));
    }

    [Fact]
    public async Task AccountSessions_FailureReleasesGateAndCorruptRegistryNeverAuthorizes()
    {
        var token = TestContext.Current.CancellationToken;
        await Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UseOwnedAccountSessionsAsync<int>(_owner.Id, _ => throw new InvalidOperationException(), token));
        Assert.Single(await _service.UseOwnedAccountSessionsAsync(_owner.Id, Task.FromResult, token));
        await File.WriteAllTextAsync(Path.Combine(_root, "castlumora", "profiles", "profiles.json"), "damaged", token);
        var called = false;
        Task<int> Record(Guid[] identities)
        {
            called = true;
            return Task.FromResult(identities.Length);
        }

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _service.UseOwnedAccountSessionsAsync(_owner.Id, Record, token));
        Assert.False(called);
    }

    private async Task<Guid> Create() => Guid.Parse((await _service.CreateAsync(_owner.Id, _owner.Id, "Kids", "kids", [_folder], TestContext.Current.CancellationToken))["id"]!.GetValue<string>());

    private static AuthorizationInfo Auth() => new() { Token = "parent-token", Client = "Castlumora Web", Version = "1.0", Device = "Browser", DeviceId = "device" };

    private ProfileService Open()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(value => value.ConfigurationDirectoryPath).Returns(_root);
        return new ProfileService(paths.Object, _users.Object, _libraries.Object, _sessions.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
