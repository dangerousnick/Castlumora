using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraPresentationControllerTests : IClassFixture<JellyfinApplicationFactory>
{
    private readonly JellyfinApplicationFactory _factory;

    public CastlumoraPresentationControllerTests(JellyfinApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DraftAndPreferences_EnforceHttpAuthorizationAndPersist()
    {
        using var anonymous = _factory.CreateClient();
        using var rejected = await anonymous.GetAsync("Castlumora/Presentation");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var config = PresentationStore.Defaults();
        config["theme"]!["accent"] = "#abcdef";
        using var saved = await admin.PutAsJsonAsync("Castlumora/Presentation/Draft", config);
        saved.EnsureSuccessStatusCode();
        var draft = await saved.Content.ReadFromJsonAsync<JsonObject>();
        using var published = await admin.PostAsJsonAsync("Castlumora/Presentation/Publish", draft);
        published.EnsureSuccessStatusCode();
        var layout = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Presentation");
        Assert.Equal("#abcdef", layout!["theme"]!["accent"]!.GetValue<string>());
        using var stale = await admin.PutAsJsonAsync("Castlumora/Presentation/Draft", config);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var created = await admin.PostAsJsonAsync("Users/New", new CreateUserByName { Name = "CastlumoraViewer" }, JsonDefaults.Options);
        created.EnsureSuccessStatusCode();
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthHelper.DummyAuthHeader);
        using var login = await viewer.PostAsJsonAsync("Users/AuthenticateByName", new AuthenticateUserByName { Username = "CastlumoraViewer", Pw = string.Empty }, JsonDefaults.Options);
        login.EnsureSuccessStatusCode();
        var authentication = await login.Content.ReadFromJsonAsync<JsonObject>();
        viewer.DefaultRequestHeaders.Remove("Authorization");
        viewer.DefaultRequestHeaders.AddAuthHeader(authentication!["AccessToken"]!.GetValue<string>());
        using var forbidden = await viewer.PutAsJsonAsync("Castlumora/Presentation/Draft", config);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenPublish = await viewer.PostAsJsonAsync("Castlumora/Presentation/Publish", config);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPublish.StatusCode);
        const string item = "11111111111111111111111111111111";
        using var preference = await viewer.PutAsync("Castlumora/Presentation/Preferences/hiddenResume/" + item + "?enabled=true", new StringContent(string.Empty));
        preference.EnsureSuccessStatusCode();
        var preferences = await viewer.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences");
        Assert.Single(preferences!["hiddenResume"]!.AsArray());
        Assert.Empty(preferences["watchlist"]!.AsArray());
        var adminPreferences = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences");
        Assert.Empty(adminPreferences!["hiddenResume"]!.AsArray());
    }
}
