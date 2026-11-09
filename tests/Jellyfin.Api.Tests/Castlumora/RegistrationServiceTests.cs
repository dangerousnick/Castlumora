using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Controllers;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class RegistrationServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "castlumora-registration-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IUserManager> _users = new();
    private readonly TestClock _clock = new();
    private readonly RegistrationService _service;
    private readonly List<string> _operations = [];

    public RegistrationServiceTests()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ConfigurationDirectoryPath).Returns(_directory);
        _users.Setup(u => u.CreateUserAsync(It.IsAny<string>(), true)).ReturnsAsync((string name, bool _) =>
        {
            _operations.Add("disabled-create");
            return new User(name, "existing-auth", "existing-reset");
        });
        _users.Setup(u => u.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).Callback(() => _operations.Add("password")).Returns(Task.CompletedTask);
        _users.Setup(u => u.UpdatePolicyAsync(It.IsAny<Guid>(), It.IsAny<UserPolicy>())).Callback<Guid, UserPolicy>((id, policy) =>
        {
            Assert.All(policy.AccessSchedules, schedule => Assert.Equal(id, schedule.UserId));
            Assert.False(policy.IsAdministrator);
            Assert.Equal("existing-auth", policy.AuthenticationProviderId);
            _operations.Add(policy.IsDisabled ? "disabled-policy" : "activation");
        }).Returns(Task.CompletedTask);
        _users.Setup(u => u.DeleteUserAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _service = new RegistrationService(paths.Object, _users.Object, _clock);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public async Task Defaults_AreClosedAndDoNotGrantLibrariesOrAdministrator()
    {
        var availability = await _service.PublicAsync();
        Assert.False(availability["enabled"]!.GetValue<bool>());
        Assert.True(availability["requireInvitation"]!.GetValue<bool>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", null));
        _users.Verify(u => u.CreateUserAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        var admin = await _service.AdminAsync();
        Assert.False(admin["settings"]!["defaultPolicy"]!["EnableAllFolders"]!.GetValue<bool>());
        Assert.False(admin["settings"]!["defaultPolicy"]!["IsAdministrator"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ConcurrentUse_IsCappedAndRawTokenNeverPersistsOrAppearsInAdminState()
    {
        await EnableAsync();
        var invite = await InviteAsync();
        var token = invite["token"]!.GetValue<string>();
        async Task<bool> Attempt(string name)
        {
            try
            {
                await _service.RegisterAsync(name, "strong-password", token);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var attempts = await Task.WhenAll(Attempt("viewer-one"), Attempt("viewer-two"));
        Assert.Single(attempts, success => success);
        Assert.Equal(new[] { "disabled-create", "password", "disabled-policy", "activation" }, _operations);
        var admin = await _service.AdminAsync();
        var stored = Assert.Single(admin["invitations"]!.AsArray());
        Assert.Equal(1, stored!["successfulUses"]!.GetValue<int>());
        Assert.Equal(0, stored["reservedUses"]!.GetValue<int>());
        Assert.Null(stored["tokenHash"]);
        var disk = await File.ReadAllTextAsync(Path.Combine(_directory, "castlumora", "registration.json"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(token, disk, StringComparison.Ordinal);
        Assert.DoesNotContain("strong-password", disk, StringComparison.Ordinal);
        Assert.DoesNotContain(token, admin.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredRevokedAndMissingInvitations_CannotCreateAccounts()
    {
        await EnableAsync();
        var revoked = await InviteAsync();
        await _service.RevokeAsync(revoked["id"]!.GetValue<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", revoked["token"]!.GetValue<string>()));
        var expired = await InviteAsync();
        _clock.Now = _clock.Now.AddHours(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", expired["token"]!.GetValue<string>()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", null));
        _users.Verify(u => u.CreateUserAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task FailedPasswordSetup_RemovesDisabledAccountAndReleasesUse()
    {
        await EnableAsync();
        var invite = await InviteAsync();
        _users.Setup(u => u.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("simulated password provider failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", invite["token"]!.GetValue<string>()));
        _users.Verify(u => u.DeleteUserAsync(It.IsAny<Guid>()), Times.Once);
        _users.Verify(u => u.UpdatePolicyAsync(It.IsAny<Guid>(), It.IsAny<UserPolicy>()), Times.Never);
        var admin = await _service.AdminAsync();
        Assert.Equal(0, admin["invitations"]![0]!["successfulUses"]!.GetValue<int>());
        Assert.Equal(0, admin["invitations"]![0]!["reservedUses"]!.GetValue<int>());
        Assert.Equal("failed", admin["registrations"]![0]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task FailedCleanup_HoldsReservationUntilAdministratorResolvesIt()
    {
        await EnableAsync();
        var invite = await InviteAsync();
        _users.Setup(u => u.ChangePassword(It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("simulated failure"));
        _users.Setup(u => u.DeleteUserAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("simulated cleanup failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync("viewer", "strong-password", invite["token"]!.GetValue<string>()));
        var admin = await _service.AdminAsync();
        Assert.Equal(1, admin["invitations"]![0]!["reservedUses"]!.GetValue<int>());
        var id = admin["registrations"]![0]!["id"]!.GetValue<string>();
        _users.Setup(u => u.GetUserByName("viewer")).Returns(new User("viewer", "auth", "reset"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ResolveAsync(id, false, Guid.NewGuid()));
        _users.Setup(u => u.GetUserByName("viewer")).Returns((User?)null);
        await _service.ResolveAsync(id, false, Guid.NewGuid());
        Assert.Equal(0, (await _service.AdminAsync())["invitations"]![0]!["reservedUses"]!.GetValue<int>());
    }

    [Fact]
    public async Task AdministratorPoliciesAndStaleSettings_AreRejected()
    {
        var settings = (await _service.AdminAsync())["settings"]!.DeepClone().AsObject();
        var stale = settings.DeepClone().AsObject();
        settings["enabled"] = true;
        await _service.SaveSettingsAsync(settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveSettingsAsync(stale));
        settings = (await _service.AdminAsync())["settings"]!.DeepClone().AsObject();
        settings["defaultPolicy"]!["IsAdministrator"] = true;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveSettingsAsync(settings));
    }

    [Fact]
    public async Task PolicyScheduleTemplates_AreBoundOnlyToTheNewUser()
    {
        var settings = (await _service.AdminAsync())["settings"]!.DeepClone().AsObject();
        settings["enabled"] = true;
        settings["requireInvitation"] = false;
        settings["defaultPolicy"]!["AccessSchedules"] = JsonNode.Parse("""[{"DayOfWeek":"Everyday","StartHour":0,"EndHour":24,"UserId":"11111111111111111111111111111111"}]""");
        await _service.SaveSettingsAsync(settings);
        await _service.RegisterAsync("scheduled-viewer", "strong-password", null);
        Assert.Contains("activation", _operations);
    }

    [Fact]
    public async Task OpenSignup_StillUsesNonAdministratorPolicyAndCanRequireActivation()
    {
        var settings = (await _service.AdminAsync())["settings"]!.DeepClone().AsObject();
        settings["enabled"] = true;
        settings["requireInvitation"] = false;
        settings["defaultPolicy"]!["IsDisabled"] = true;
        await _service.SaveSettingsAsync(settings);
        await _service.RegisterAsync("awaiting-approval", "strong-password", null);
        Assert.DoesNotContain("activation", _operations);
        Assert.Equal(2, _operations.Count(operation => operation == "disabled-policy"));
    }

    [Fact]
    public async Task CustomManagerWithoutDisabledProvisioning_FailsClosedAndPreservesOldMethod()
    {
        var legacy = new Mock<IUserManager> { CallBase = true };
        var user = new User("legacy", "auth", "reset");
        legacy.Setup(manager => manager.CreateUserAsync("legacy")).ReturnsAsync(user);
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Object.CreateUserAsync("viewer", true));
        Assert.Same(user, await legacy.Object.CreateUserAsync("legacy", false));
        legacy.Verify(manager => manager.CreateUserAsync("viewer"), Times.Never);
    }

    [Fact]
    public void AttemptLimiter_RecoversAfterWindowAndSeparatesAddresses()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.True(_service.TryAttempt("127.0.0.1"));
        }

        Assert.False(_service.TryAttempt("127.0.0.1"));
        Assert.True(_service.TryAttempt("::1"));
        _clock.Now = _clock.Now.AddMinutes(1);
        Assert.True(_service.TryAttempt("127.0.0.1"));
    }

    [Theory]
    [InlineData(nameof(CastlumoraRegistrationController.GetAdmin))]
    [InlineData(nameof(CastlumoraRegistrationController.SaveSettings))]
    [InlineData(nameof(CastlumoraRegistrationController.CreateInvitation))]
    [InlineData(nameof(CastlumoraRegistrationController.RevokeInvitation))]
    [InlineData(nameof(CastlumoraRegistrationController.ResolveReservation))]
    public void InvitationAndPolicyActions_RequireAdministrator(string method)
    {
        Assert.Contains(typeof(CastlumoraRegistrationController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>(), attribute => attribute.Policy == Policies.RequiresElevation);
    }

    private async Task EnableAsync()
    {
        var settings = (await _service.AdminAsync())["settings"]!.DeepClone().AsObject();
        settings["enabled"] = true;
        await _service.SaveSettingsAsync(settings);
    }

    private Task<JsonObject> InviteAsync() => _service.CreateInviteAsync(
        new JsonObject
        {
            ["name"] = "Test invitation",
            ["expiresAt"] = _clock.Now.AddHours(1).ToString("O"),
            ["maxUses"] = 1
        },
        Guid.NewGuid());

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
