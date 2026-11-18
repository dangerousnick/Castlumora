using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraRegistrationControllerTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraRegistrationControllerTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Signup_EnforcesOptInInvitationCapPoliciesAndAdministratorBoundaries()
    {
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthHelper.DummyAuthHeader);
        var availability = await anonymous.GetFromJsonAsync<JsonObject>("Castlumora/Registration", TestContext.Current.CancellationToken);
        Assert.False(availability!["enabled"]!.GetValue<bool>());
        using var closed = await anonymous.PostAsJsonAsync("Castlumora/Registration", new { username = "SignupViewer", password = "strong-fixture-password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, closed.StatusCode);
        using var privateDenied = await anonymous.GetAsync("Castlumora/Registration/Admin", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, privateDenied.StatusCode);

        var state = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Registration/Admin", TestContext.Current.CancellationToken);
        var settings = state!["settings"]!.DeepClone().AsObject();
        settings["enabled"] = true;
        settings["defaultPolicy"]!["AccessSchedules"] = JsonNode.Parse("""[{"DayOfWeek":"Everyday","StartHour":0,"EndHour":24,"UserId":"11111111111111111111111111111111"}]""");
        using var enabled = await admin.PutAsJsonAsync("Castlumora/Registration/Settings", settings, TestContext.Current.CancellationToken);
        enabled.EnsureSuccessStatusCode();
        using var invitation = await admin.PostAsJsonAsync("Castlumora/Registration/Invitations", new { name = "Fixture invitation", expiresAt = System.DateTimeOffset.UtcNow.AddHours(1).ToString("O"), maxUses = 1 }, TestContext.Current.CancellationToken);
        invitation.EnsureSuccessStatusCode();
        var created = await invitation.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        var token = created!["token"]!.GetValue<string>();
        using var registered = await anonymous.PostAsJsonAsync("Castlumora/Registration", new { username = "SignupViewer", password = "strong-fixture-password", invitation = token, policy = new { IsAdministrator = true, EnableAllFolders = true } }, TestContext.Current.CancellationToken);
        registered.EnsureSuccessStatusCode();
        using var reused = await anonymous.PostAsJsonAsync("Castlumora/Registration", new { username = "AnotherViewer", password = "strong-fixture-password", invitation = token }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        var publicError = await reused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(token, publicError, System.StringComparison.Ordinal);
        Assert.DoesNotContain("AnotherViewer", publicError, System.StringComparison.Ordinal);

        using var blankPassword = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new AuthenticateUserByName { Username = "SignupViewer", Pw = string.Empty }, JsonDefaults.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, blankPassword.StatusCode);
        using var login = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new AuthenticateUserByName { Username = "SignupViewer", Pw = "strong-fixture-password" }, JsonDefaults.Options, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var authenticated = await login.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.False(authenticated!["User"]!["Policy"]!["IsAdministrator"]!.GetValue<bool>());
        Assert.False(authenticated["User"]!["Policy"]!["EnableAllFolders"]!.GetValue<bool>());
        Assert.Empty(authenticated["User"]!["Policy"]!["EnabledFolders"]!.AsArray());
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.AddAuthHeader(authenticated["AccessToken"]!.GetValue<string>());
        using var invitationDenied = await viewer.PostAsJsonAsync("Castlumora/Registration/Invitations", new { name = "forbidden" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, invitationDenied.StatusCode);
        using var settingsDenied = await viewer.PutAsJsonAsync("Castlumora/Registration/Settings", settings, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, settingsDenied.StatusCode);
        using var auditDenied = await viewer.GetAsync("Castlumora/Registration/Admin", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, auditDenied.StatusCode);

        var redacted = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Registration/Admin", TestContext.Current.CancellationToken);
        Assert.DoesNotContain(token, redacted!.ToJsonString(), System.StringComparison.Ordinal);
        Assert.Null(redacted["invitations"]![0]!["tokenHash"]);
        Assert.Equal(1, redacted["invitations"]![0]!["successfulUses"]!.GetValue<int>());
        Assert.Equal("completed", redacted["registrations"]![0]!["status"]!.GetValue<string>());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var missing = await anonymous.PostAsJsonAsync("Castlumora/Registration", new { username = "NoInvitation", password = "strong-fixture-password" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        }

        using var throttled = await anonymous.PostAsJsonAsync("Castlumora/Registration", new { username = "NoInvitation", password = "strong-fixture-password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
