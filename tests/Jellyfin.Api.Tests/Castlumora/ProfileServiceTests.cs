using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
