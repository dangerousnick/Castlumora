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
        using var rejected = await anonymous.GetAsync("Castlumora/Presentation", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        var config = PresentationStore.Defaults();
        config["theme"]!["accent"] = "#abcdef";
        config["categories"]!.AsArray().Add(JsonNode.Parse("""{"id":"empty","title":"Empty","rules":{"itemIds":[],"folderIds":[],"genres":[],"studios":[]}}"""));
        config["categories"]!.AsArray().Add(JsonNode.Parse("""{"id":"restricted","title":"Restricted","userIds":["11111111111111111111111111111111"],"rules":{"itemIds":[],"folderIds":[],"genres":[],"studios":[]}}"""));
        using var saved = await admin.PutAsJsonAsync("Castlumora/Presentation/Draft", config, TestContext.Current.CancellationToken);
        saved.EnsureSuccessStatusCode();
        var draft = await saved.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        using var published = await admin.PostAsJsonAsync("Castlumora/Presentation/Publish", draft, TestContext.Current.CancellationToken);
        published.EnsureSuccessStatusCode();
        var layout = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Presentation", TestContext.Current.CancellationToken);
        Assert.Equal("#abcdef", layout!["theme"]!["accent"]!.GetValue<string>());
        using var stale = await admin.PutAsJsonAsync("Castlumora/Presentation/Draft", config, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var created = await admin.PostAsJsonAsync("Users/New", new CreateUserByName { Name = "CastlumoraViewer" }, JsonDefaults.Options, TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthHelper.DummyAuthHeader);
        using var login = await viewer.PostAsJsonAsync("Users/AuthenticateByName", new AuthenticateUserByName { Username = "CastlumoraViewer", Pw = string.Empty }, JsonDefaults.Options, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var authentication = await login.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        viewer.DefaultRequestHeaders.Remove("Authorization");
        viewer.DefaultRequestHeaders.AddAuthHeader(authentication!["AccessToken"]!.GetValue<string>());
        using var historyDenied = await viewer.GetAsync("Castlumora/Presentation/History", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, historyDenied.StatusCode);
        var history = await admin.GetFromJsonAsync<JsonArray>("Castlumora/Presentation/History", TestContext.Current.CancellationToken);
        Assert.Single(history!);
        var emptyCategory = await viewer.GetFromJsonAsync<JsonObject>("Castlumora/Categories/empty/Items", TestContext.Current.CancellationToken);
        Assert.Equal(0, emptyCategory!["TotalRecordCount"]!.GetValue<int>());
        using var hiddenCategory = await viewer.GetAsync("Castlumora/Categories/restricted/Items", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, hiddenCategory.StatusCode);
        using var badPage = await viewer.GetAsync("Castlumora/Categories/empty/Items?limit=1000", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        using var forbidden = await viewer.PutAsJsonAsync("Castlumora/Presentation/Draft", config, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenPublish = await viewer.PostAsJsonAsync("Castlumora/Presentation/Publish", config, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPublish.StatusCode);
        const string item = "11111111111111111111111111111111";
        using var preference = await viewer.PutAsync("Castlumora/Presentation/Preferences/hiddenResume/" + item + "?enabled=true", new StringContent(string.Empty), TestContext.Current.CancellationToken);
        preference.EnsureSuccessStatusCode();
        var preferences = await viewer.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences", TestContext.Current.CancellationToken);
        Assert.Single(preferences!["hiddenResume"]!.AsArray());
        Assert.Empty(preferences["watchlist"]!.AsArray());
        var adminPreferences = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Presentation/Preferences", TestContext.Current.CancellationToken);
        Assert.Empty(adminPreferences!["hiddenResume"]!.AsArray());
    }
}
