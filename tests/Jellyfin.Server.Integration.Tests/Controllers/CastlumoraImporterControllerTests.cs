using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraImporterControllerTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraImporterControllerTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RuntimeHealthAndInstall_AreAdministratorOnly_AndMissingBundleCannotExecuteInputs()
    {
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthHelper.DummyAuthHeader);
        using var denied = await anonymous.GetAsync("Castlumora/Imports/Runtime", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var created = await admin.PostAsJsonAsync("Users/New", new { Name = "ImporterRuntimeViewer" }, TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();
        using var login = await anonymous.PostAsJsonAsync("Users/AuthenticateByName", new AuthenticateUserByName { Username = "ImporterRuntimeViewer", Pw = string.Empty }, JsonDefaults.Options, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var authentication = await login.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.AddAuthHeader(authentication!["AccessToken"]!.GetValue<string>());
        using var viewerStatus = await viewer.GetAsync("Castlumora/Imports/Runtime", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, viewerStatus.StatusCode);
        using var viewerInstall = await viewer.PostAsync("Castlumora/Imports/Runtime/Install", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, viewerInstall.StatusCode);

        var state = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Runtime", TestContext.Current.CancellationToken);
        Assert.False(state!["ready"]!.GetValue<bool>());
        Assert.False(state["downloadJobsAvailable"]!.GetValue<bool>());
        Assert.False(state["bundleAvailable"]!.GetValue<bool>());
        using var install = await admin.PostAsJsonAsync("Castlumora/Imports/Runtime/Install", new { executable = "/untrusted/command", url = "http://127.0.0.1/private", arguments = new[] { "--exec", "untrusted" } }, TestContext.Current.CancellationToken);
        Assert.True(install.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotImplemented);
        var unchanged = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Runtime", TestContext.Current.CancellationToken);
        Assert.False(unchanged!["ready"]!.GetValue<bool>());
    }
}
